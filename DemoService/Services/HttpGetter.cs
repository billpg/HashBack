using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DemoService.Services;

public interface IHttpGetter
{
    Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req);
}

public class HttpGetter : IHttpGetter
{
    private readonly ServiceData data;
    private readonly IIpFilter ipFilter;

    public HttpGetter(ServiceData data, IIpFilter ipFilter)
    {
        this.data = data;
        this.ipFilter = ipFilter;
    }

    public async Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req)
    {
        /* First, validate the URL. */
        ValidateUrlOrThrow(req.Url);

        /* Connect TCP and handshake TLS. The finally block with close them. */
        (var tcpcli, var netstr) = await ConnectHttp(req.Url);
        try
        {
            /* Build the HTTP request. */
            var requestLines = new List<string>
            {
                $"GET {req.Url.PathAndQuery} HTTP/1.1",
                $"Host: {req.Url.Host}",
                "Connection: close",
                "Accept-Encoding: plain",
                "User-Agent: demo.hashback.dev"
            };
            requestLines.AddRange(req.Headers.Select(kv => $"{kv.Key}: {kv.Value}"));
            var request = Encoding.ASCII.GetBytes(string.Join("\r\n", requestLines) + "\r\n\r\n");

            /* Send the request. */
            await netstr.WriteAsync(request);

            /* Read the response. */
            byte[] respBytes = new byte[1000];
            int bytesIn = await netstr.ReadAsync(respBytes, 0, respBytes.Length);
            string respAsString = Encoding.ASCII.GetString(respBytes, 0, bytesIn);
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
            return resp;
        }
        finally
        {
            netstr.Dispose();
            tcpcli.Dispose();
        }
    }

    private async Task<(TcpClient tcpcli, Stream netstr)> ConnectHttp(Uri uri)
    {
        /* Check if this is a localhost allowance */
        bool isDebug = ServiceData.AllowGetLocalhost && uri.Scheme == "http" && uri.Host == "localhost";

        /* Validate the URL is acceptable. */
        if (uri.Scheme != "https" && !isDebug)
            throw new ApplicationException("URL must be HTTPS only.");
        if (uri.Port != 443 && !isDebug)
            throw new ApplicationException("URL must be port 443.");

        /* Make all the precautions for public use. */
        var remoteIp = await ResolveDomain(uri.Host, isDebug);

        /* Connect TCP. */
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(remoteIp, uri.Port);
        }
        catch (SocketException)
        {
            throw new BadRequestException(
                "External URL not available.", 
                $"Can't connect to {uri} ({remoteIp})");
        }
        var netstr = tcp.GetStream();

        /* Handshake TLS. */
        if (uri.Scheme == "https")
        {
            var tls = new SslStream(netstr); // TODO Capture cert.
            await tls.AuthenticateAsClientAsync(uri.Host);
            return (tcp, tls);
        }
        return (tcp, netstr);
    }

    private async Task<IPAddress> ResolveDomain(string host, bool isDebug)
    {
        /* Shortcut the one acceptable use of localhost. */
        if (host == "localhost" && isDebug)
            return IPAddress.Loopback;

        /* Load the various IPs and filter. (IsAcceptable will internally
         * update that remote IP's quota.) */
        var ip = (await Dns.GetHostAddressesAsync(host)).First();
        if (ipFilter.IsAcceptable(ip))
            return ip;

        /* 400 if the IP is not acceptable. */
        throw new ApplicationException($"IP address ({ip}) of host ({host}) is not acceptable.");
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
