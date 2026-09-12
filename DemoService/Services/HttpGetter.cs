using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using billpg.SpartanHttpClient;
using DemoService.Data;

namespace DemoService.Services;

public interface IHttpGetter
{
    /// <summary>
    /// Fetches url on this service's behalf, logging the attempt to OutboundGetLog first -
    /// callerIp and source (Hello or Call) identify which caller, hitting which of this
    /// service's own endpoints, triggered it.
    /// </summary>
    Task<SpartanResponse> GetAsync(Uri url, string? authorizationHeader, IPAddress callerIp, OutboundGetSource source);
}

/// <summary>
/// Decides whether a remote TLS certificate is acceptable. Given the certificate, its
/// chain, and the policy errors SslStream's own default validation would have raised.
/// The default handler simply returns true only when sslPolicyErrors is None - i.e.
/// exactly what SslStream would have accepted with no custom callback at all - so
/// overriding this is opt-in, for cases such as certificate pinning.
/// </summary>
public delegate bool IsCertificateAcceptableDelegate(
    Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors);

/// <summary>
/// Fetches a single GET request on DemoService's behalf. The actual socket/TLS/HTTP work
/// is delegated to billpg.SpartanHttpClient; this class supplies everything that library
/// doesn't do itself - the stricter public-use URL policy (HTTPS, port 443, no IP
/// literals, dotted domains only), the SSRF-guarding multi-candidate DNS resolution that
/// walks every answer through <see cref="IIpFilter"/> until one is acceptable, and the
/// localhost/AllowGetLocalhost bypass used by tests.
/// </summary>
public class HttpGetter : IHttpGetter
{
    private readonly IIpFilter ipFilter;
    private readonly ICallPermissionChecker permissionChecker;
    private readonly IOutboundGetLog outboundGetLog;
    private readonly TimeSpan timeout;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> dnsLookup;
    private readonly IsCertificateAcceptableDelegate isCertificateAcceptable;

    /// <summary>
    /// Constructs a new HttpGetter. The optional timeout bounds the entire fetch - DNS
    /// resolution, TCP connect, TLS handshake, and the request/response exchange - so a
    /// slow or unresponsive remote server (accidental or malicious) can't tie up this
    /// connection indefinitely. Defaults to ten seconds; pass a shorter value in tests
    /// that need to exercise the timeout without waiting for the real default.
    /// The optional dnsLookup replaces the real DNS resolver, for tests that need to
    /// supply a specific, synthetic set of addresses for a host.
    /// The optional isCertificateAcceptable replaces the default TLS certificate check
    /// (see <see cref="IsCertificateAcceptableDelegate"/>).
    /// </summary>
    public HttpGetter(
        ServiceData data,
        IIpFilter ipFilter,
        ICallPermissionChecker permissionChecker,
        IOutboundGetLog outboundGetLog,
        TimeSpan? timeout = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? dnsLookup = null,
        IsCertificateAcceptableDelegate? isCertificateAcceptable = null)
    {
        this.ipFilter = ipFilter;
        this.dnsLookup = dnsLookup ?? Dns.GetHostAddressesAsync;
        this.permissionChecker = permissionChecker;
        this.outboundGetLog = outboundGetLog;
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
        this.isCertificateAcceptable = isCertificateAcceptable ?? DefaultIsCertificateAcceptable;
    }

    private static bool DefaultIsCertificateAcceptable(
        Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;

    public async Task<SpartanResponse> GetAsync(Uri url, string? authorizationHeader, IPAddress callerIp, OutboundGetSource source)
    {
        /* Perform some basic validation on the URL before we run the GET.
         * Will throw if not acceptable. */
        ValidateUrlOrThrow(url);
        return await FetchAsync(url, authorizationHeader, callerIp, source);
    }

    /// <summary>
    /// Does the actual fetch, skipping the public-use URL policy in <see cref="ValidateUrlOrThrow"/>.
    /// Internal, and only exists so certificate-handling tests can point this at an
    /// https://localhost URL on a non-443 port - something ValidateUrlOrThrow never lets
    /// through regardless of AllowGetLocalhost, since "localhost" has no dot and that
    /// bypass only covers plain HTTP. callerIp/source default to a filler value, since
    /// those tests don't care about the logged attempt itself.
    /// </summary>
    internal async Task<SpartanResponse> FetchAsync(
        Uri url, string? authorizationHeader = null, IPAddress? callerIp = null, OutboundGetSource source = OutboundGetSource.Hello)
    {
        var effectiveCallerIp = callerIp ?? IPAddress.Loopback;

        /* Check the target service's JSON permission. */
        if (!await permissionChecker.IsCallPermittedAsync(url, effectiveCallerIp))
            throw new BadRequestException(
                "Target not opted in.",
                "This service will only call targets that have explicitly granted permission via " +
                $"<https://{url.Host}/.well-known/demo-hashback-dev.json>. " +
                "See https://demo.hashback.dev/permit for details.");

        /* Log the attempt regardless of whether the fetch itself goes on to succeed. */
        await outboundGetLog.LogAsync(effectiveCallerIp, source, url);

        /* Make the GET request. */
        var spartanRequest = new SpartanRequest(url)
            .WithTimeout(timeout)
            .WithMaxResponseBytes(1000)
            .WithHeader("Authorization", authorizationHeader)
            .WithHeader("User-Agent", "demo.hashback.dev")
            .WithIpLookupHandler(ResolveDomainToSingleIp)
            .WithCertificateValidator(MyIsCertificateAcceptable);

        try
        {
            return await spartanRequest.Run();
        }
        catch (SpartanHttpException ex)
        {
            throw new BadRequestException(ex.Title, ex.Message);
        }
    }

    internal async Task<IPAddress> ResolveDomainToSingleIp(string host, CancellationToken cancellationToken)
    {
        /* Shortcut the one acceptable use of localhost - matching the same case
         * ValidateUrlOrThrow already let through. Without this, "localhost" would still
         * resolve via the real DNS lookup to a loopback address, which IpFilter always
         * rejects regardless of AllowGetLocalhost. */
        if (ServiceData.AllowGetLocalhost && host == "localhost")
            return IPAddress.Loopback;

        /* Load the various IPs for this host and use the first one that passes the IP
         * filter, rather than assuming the resolver's first answer is usable - a
         * legitimate dual-stack or multi-homed host can have some addresses this filter
         * would reject (for example, a link-local IPv6 entry) alongside a usable one.
         * (IsAcceptable will internally update that address's quota.) */
        var candidates = await dnsLookup(host, cancellationToken);
        foreach (var ip in candidates)
        {
            if (ipFilter.IsAcceptable(ip))
                return ip;
        }

        /* None of the resolved addresses were acceptable. */
        throw new ApplicationException(
            $"None of the {candidates.Length} IP address(es) for host ({host}) are acceptable.");
    }

    private bool MyIsCertificateAcceptable(Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        return sslPolicyErrors == SslPolicyErrors.None || isCertificateAcceptable(url, certificate, chain, sslPolicyErrors);
    }

    private static void ValidateUrlOrThrow(Uri url)
    {
        /* Allow HTTP for localhost only, and only when allowed. */
        if (ServiceData.AllowGetLocalhost &&
            url.Scheme == Uri.UriSchemeHttp &&
            url.Host == "localhost")
            return;

        BadRequestException Ex(string detail)
            => new("URL not acceptable.", detail);

        /* Reject anything other than HTTPS. */
        if (url.Scheme != Uri.UriSchemeHttps)
            throw Ex($"{url.Scheme} URLs are not accepted.");

        /* If the port is anything other than 443, reject it. */
        if (url.Port != 443)
            throw Ex($"URL must be HTTPS and port 443.");

        /* If the host is less than five characters, reject it. */
        if (url.Host.Length < 5)
            throw Ex("URL host is too short.");

        /* If the URL contains any non-ascii characters, reject it. */
        if (url.Host.Any(c => c > 127) || url.PathAndQuery.Contains('%'))
            throw Ex("URL contains non-ASCII.");

        /* If the host is an IP address, reject it. */
        if (IPAddress.TryParse(url.Host, out _))
            throw Ex("Host must be for a domain.");

        /* If the host starts or ends with a dot, reject it. */
        if (url.Host.StartsWith('.') || url.Host.EndsWith('.'))
            throw Ex("Host must not start or end with a dot.");

        /* If the host is a single undotted string, reject it. */
        if (!url.Host.Contains('.'))
            throw Ex("URL must be for a domain with dots.");

        /* Anything else is considered secure. */
        return;
    }
}
