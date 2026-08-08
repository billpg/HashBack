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
        /* Validate the URL is acceptable. */
        bool isDebug = uri.Authority == data.ConfigServiceHost;
        if (uri.Scheme != "https" && !isDebug)
            throw new ApplicationException("URL must be HTTPS only.");
        if (uri.Port != 443 && !isDebug)
            throw new ApplicationException("URL must be port 443.");

        /* Make all the precautions for public use. */
        var remoteIp = await ResolveDomain(uri.Host, isDebug);

        /* Connect TCP. */
        var tcp = new TcpClient();
        await tcp.ConnectAsync(remoteIp, uri.Port);
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

    private (string httpVersion, int statusCode, string statusDescription) ParseHttpBanner(string? banner)
    {
        if (string.IsNullOrWhiteSpace(banner))
            throw new ApplicationException("Empty or missing HTTP response banner.");

        // Split into at most 3 parts: version, status code, and the rest as description.
        var parts = banner.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ApplicationException($"Invalid HTTP banner: '{banner}'");

        var httpVersion = parts[0];
        if (!httpVersion.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException($"Invalid HTTP version in banner: '{banner}'");

        if (!int.TryParse(parts[1], out int statusCode))
            throw new ApplicationException($"Invalid status code in banner: '{banner}'");

        var statusDescription = parts.Length >= 3 ? parts[2] : string.Empty;

        return (httpVersion, statusCode, statusDescription);
    }
}
