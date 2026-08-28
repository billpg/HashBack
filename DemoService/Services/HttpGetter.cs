using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace DemoService.Services;

public interface IHttpGetter
{
    Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req);
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

public class HttpGetter : IHttpGetter
{
    private readonly ServiceData data;
    private readonly IIpFilter ipFilter;
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
        TimeSpan? timeout = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? dnsLookup = null,
        IsCertificateAcceptableDelegate? isCertificateAcceptable = null)
    {
        this.data = data;
        this.ipFilter = ipFilter;
        this.dnsLookup = dnsLookup ?? Dns.GetHostAddressesAsync;
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
        this.isCertificateAcceptable = isCertificateAcceptable ?? DefaultIsCertificateAcceptable;
    }

    private static bool DefaultIsCertificateAcceptable(
        Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;

    public async Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req)
    {
        /* First, validate the URL. */
        ValidateUrlOrThrow(req.Url);

        /* A single deadline covers every network step below. */
        using var cts = new CancellationTokenSource(timeout);
        var cancellationToken = cts.Token;

        TcpClient? tcpcli = null;
        Stream? netstr = null;
        string? certificateHash = null;
        try
        {
            /* Connect TCP and handshake TLS. */
            (tcpcli, netstr, certificateHash) = await ConnectHttp(req.Url, cancellationToken);

            /* Build and send the HTTP request. */
            var requestLines = new List<string>
            {
                $"GET {req.Url.PathAndQuery} HTTP/1.1",
                $"Host: {req.Url.Host}",
                "Connection: close",
                "Accept-Encoding: identity",
                "User-Agent: demo.hashback.dev"
            };
            requestLines.AddRange(req.Headers.Select(kv => $"{kv.Key}: {kv.Value}"));
            var request = Encoding.ASCII.GetBytes(string.Join("\r\n", requestLines) + "\r\n\r\n");
            await netstr.WriteAsync(request, cancellationToken);

            /* Read the response. Cut everything after the first 1k. */
            string respAsString = await LoadBytesFromStream(netstr, cancellationToken, 1000); 
            var respStream = new StringReader(respAsString);

            /* Churn the lines through the response builder state machine. */
            var resp = new SimpleHttpResponse();
            while (true)
            {
                string? line = respStream.ReadLine();
                if (line == null)
                    break;
                resp = resp.WithResponseLine(line);
            }
            return resp.WithRemoteCertificateHash(certificateHash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new BadRequestException(
                "External URL not available.",
                $"Timed out communicating with {req.Url} after {timeout.TotalSeconds:0.#} seconds.");
        }
        finally
        {
            netstr?.Dispose();
            tcpcli?.Dispose();
        }
    }

    private async Task<string> LoadBytesFromStream(Stream netstr, CancellationToken cancellationToken, int maxByteCount)
    {
        byte[] respBytes =  new byte[maxByteCount];
        int freeIndex = 0;
        while (freeIndex < maxByteCount)
        {
            int bytesIn = await netstr.ReadAsync(respBytes.AsMemory(freeIndex), cancellationToken);
            if (bytesIn <= 0)
                break;
            freeIndex += bytesIn;
        }
        return Encoding.ASCII.GetString(respBytes, 0, freeIndex);
    }

    internal async Task<(TcpClient tcpcli, Stream netstr, string? certificateHash)> ConnectHttp(Uri uri, CancellationToken cancellationToken)
    {
        /* Check if this is a localhost allowance. (Note: ValidateUrlOrThrow, the gate in
         * front of the public GetAsync entry point, never lets an https://localhost URL
         * through regardless of this flag - "localhost" has no dot and non-443 ports are
         * rejected there unconditionally - so allowing https here only matters for tests
         * that call ConnectHttp directly.) */
        bool isDebug = ServiceData.AllowGetLocalhost
            && (uri.Scheme == "http" || uri.Scheme == "https")
            && uri.Host == "localhost";

        /* Validate the URL is acceptable. */
        if (uri.Scheme != "https" && !isDebug)
            throw new ApplicationException("URL must be HTTPS only.");
        if (uri.Port != 443 && !isDebug)
            throw new ApplicationException("URL must be port 443.");

        /* Make all the precautions for public use. */
        var remoteIp = await ResolveDomain(uri.Host, isDebug, cancellationToken);

        /* Connect TCP. */
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(remoteIp, uri.Port).WaitAsync(cancellationToken);
        }
        catch (SocketException)
        {
            tcp.Dispose();
            throw new BadRequestException(
                "External URL not available.",
                $"Can't connect to {uri} ({remoteIp})");
        }
        catch
        {
            /* Covers a timeout (OperationCanceledException) and anything else - dispose
             * what we've opened so far and let the caller decide how to report it. */
            tcp.Dispose();
            throw;
        }
        var netstr = tcp.GetStream();

        /* Handshake TLS. */
        if (uri.Scheme == "https")
        {
            string? certificateHash = null;
            var tls = new SslStream(netstr, leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                {
                    /* No certificate at all is never acceptable. */
                    if (certificate == null)
                        return false;

                    /* Record the certificate's hash regardless of the outcome, so callers
                     * can see what was actually presented even if it gets rejected. Only
                     * dispose the X509Certificate2 if we had to construct it ourselves -
                     * one handed to us already as X509Certificate2 belongs to SslStream. */
                    X509Certificate2? owned = null;
                    try
                    {
                        var cert2 = certificate as X509Certificate2 ?? (owned = new X509Certificate2(certificate));
                        certificateHash = Convert.ToBase64String(cert2.GetCertHash(HashAlgorithmName.SHA256));
                        return isCertificateAcceptable(uri, cert2, chain, sslPolicyErrors);
                    }
                    finally
                    {
                        owned?.Dispose();
                    }
                });
            try
            {
                await tls.AuthenticateAsClientAsync(uri.Host).WaitAsync(cancellationToken);
            }
            catch (AuthenticationException ex)
            {
                tls.Dispose();
                tcp.Dispose();
                string certDetail = certificateHash != null
                    ? $" Presented certificate SHA-256 (Base64): {certificateHash}."
                    : "";
                throw new BadRequestException(
                    "External URL not available.",
                    $"TLS handshake with {uri} was rejected: {ex.Message}{certDetail}");
            }
            catch
            {
                /* Covers a timeout (OperationCanceledException) and anything else - dispose
                 * what we've opened so far and let the caller decide how to report it. */
                tls.Dispose();
                tcp.Dispose();
                throw;
            }
            return (tcp, tls, certificateHash);
        }
        return (tcp, netstr, null);
    }

    internal async Task<IPAddress> ResolveDomain(string host, bool isDebug, CancellationToken cancellationToken)
    {
        /* Shortcut the one acceptable use of localhost. */
        if (host == "localhost" && isDebug)
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

        /* If the host is less than five charcters, reject it. */
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
