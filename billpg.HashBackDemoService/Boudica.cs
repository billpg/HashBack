using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackDemoService;

public class Boudica
{
    private readonly CancellationTokenSource cancel;
    private readonly TcpListener listen;
    private readonly Task serviceTask;

    private Boudica(CancellationTokenSource cancel, TcpListener listen, Task serviceTask)
    {
        this.cancel = cancel;
        this.listen = listen;
        this.serviceTask = serviceTask;
    }

    /// <summary>
    /// Gets the port this service is listening on.
    /// </summary>
    public int ListenPort => ((IPEndPoint)listen.LocalEndpoint).Port;

    public delegate BoudicaResponse RequestHandler(BoudicaRequest req);

    public static Boudica Start(int port, RequestHandler handler)
    {
        /* Set up a listener and something to stop it. */
        var cancel = new CancellationTokenSource();
        var listen = new TcpListener(IPAddress.Loopback, port);

        /* Start listening until the first await. */
        var serviceTask = TaskMain();

        /* Was there an exception prior to the await?
         * If so, rethrow the exception. */
        if (serviceTask.IsFaulted)
            throw serviceTask.Exception!.InnerException!;

        /* Return a nice container to the caller. */
        return new Boudica(cancel, listen, serviceTask);

        /* Service Task entry point. */
        async Task TaskMain()
        {
            /* Switch on the listener. */
            listen.Start();

            /* Make a list of open tasks for waiting
             * at the end. */
            var openTasks = new List<Task>();

            /* Keep looping until cancelled. */
            while (!cancel.IsCancellationRequested)
            {
                /* Remove a single completed task from the open collection. */
                var disposeTask = openTasks.Where(t => t.IsCompleted).FirstOrDefault();
                if (disposeTask != null)
                {
                    await disposeTask;
                    disposeTask.Dispose();
                    openTasks.Remove(disposeTask);
                }

                /* Wait for a connection. */
                TcpClient tcp;
                try
                {
                    tcp = await listen.AcceptTcpClientAsync(cancel.Token);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }

                /* Pass control to the handler as a task and store. */
                var handlerTask = HandleConnection(tcp, handler);
                openTasks.Add(handlerTask);               
            }

            /* Wait for all the open tasks to end. */
            Task.WaitAll(openTasks);
            listen.Stop();
        }
    }

    private static async Task HandleConnection(
        TcpClient tcp, RequestHandler handler)
    {
        /* Open a new stream from the caller. */
        using var stream = tcp.GetStream();

        /* Open a try block to catc any HTTP excpeptions. */
        BoudicaResponse resp;
        try
        {
            var req = await ParseRequest(stream);
            resp = handler(req);
            await SendResponse(stream, resp);
        }
        catch (BoudicaExceptionBase ex)
        {
            resp = ex.ToResponse();
        }
    }

    private static async Task<BoudicaRequest> ParseRequest(NetworkStream stream)
    { 
        var reader = new BufferReader(stream);

        /* Read and parse the HTTP banner line. */
        var banner = await reader.ReadLineAsString();
        (string httpMethod, string resource, string httpVersion) 
            = ParseHttpBanner(banner);

        /* Keep looping over the header until an empty line. */
        var headerLines = new List<string>();
        while (true)
        {
            var headerLine = await reader.ReadLineAsString();
            if (string.IsNullOrEmpty(headerLine))
                break;
            headerLines.Add(headerLine);
        }

        /* Loop header into dictionary. */
        var headerDict = new Dictionary<string, string>();
        foreach (var line in headerLines)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                throw new BadRequestException("Header is missing a colon.");
            string headerName = line.Substring(0, colonIndex);
            string headerValue = line.Substring(colonIndex + 1).TrimStart(' ');
            AddToHeader(headerDict, headerName, headerValue);
        }

        /* Load the body. */
        byte[]? requestBody = null;
        if (headerDict.TryGetValue("Content-Transfer-Encoding", out string? cte))
        {
            if (cte != "chunked")
                throw new BadRequestException("Only Chunked and Content-Length modes suppored.");
        }
        else if (headerDict.TryGetValue("Content-Length", out string? contentLength))
        {
            int bodyLength = int.Parse(contentLength);
            if (bodyLength > 64 * 1024)
                throw new BadRequestException("Request body too large.");
            if (bodyLength < 0)
                throw new BadRequestException("Request body can't have negative length.");

            requestBody = new byte[bodyLength];
            int bodyIndex = 0;
            while (bodyIndex < bodyLength)
            {
                var bodyBlock = await reader.ReadBlock(bodyLength - bodyIndex);
                Buffer.BlockCopy(bodyBlock, 0, requestBody, bodyIndex, bodyBlock.Length);
                bodyIndex += bodyBlock.Length;
            }
        }

        return new BoudicaRequest(httpMethod, resource, httpVersion, headerDict, requestBody);
    }

    private static async Task SendResponse(NetworkStream stream, BoudicaResponse resp)
    {
        var respText = new List<string>
            { $"HTTP/1.1 {resp.StatusCode} {resp.StatusDescription}" };
        respText.AddRange(resp.Headers.Select(kv => $"{kv.Key}: {kv.Value}"));
        respText.Add("");
        string respAsString = string.Concat(respText.Select(s => s + "\r\n"));
        var respAsBytes = Encoding.UTF8.GetBytes(respAsString);
        await stream.WriteAsync(respAsBytes, 0, respAsBytes.Length);
        await stream.WriteAsync(resp.BodyBytes, 0, resp.BodyBytes.Length);
    }

    private static void AddToHeader(Dictionary<string, string> header, string name, string value)
    {
        int counter = 0;
        while (true)
        {
            var suffix = "";
            if (counter > 0)
                suffix = $"({counter})";
            string tryKey = name + suffix;
            if (header.ContainsKey(tryKey))
            {
                counter++;
                continue;
            }
            header.Add(tryKey, value);
            break;
        }
    }

    private static (string httpMethod, string resource, string httpVersion)
        ParseHttpBanner(string? banner)
    {
        if (string.IsNullOrEmpty(banner))
            throw new BadRequestException("Missing banner line.");
        int firstSp = banner.IndexOf(' ');
        int lastSp = banner.LastIndexOf(' ');
        if (firstSp < 0 || lastSp <= firstSp)
            throw new BadRequestException("Malformed banner line.");
        string httpMethod = banner.Substring(0, firstSp);
        string resource = banner.Substring(firstSp+1, lastSp-firstSp-1);
        string httpVersion = banner.Substring(lastSp+1);
        return (httpMethod, resource.Trim(), httpVersion);
    }


    public async Task Stop()
    {
        this.cancel.Cancel();
        await serviceTask;
        serviceTask.Dispose();
    }
}

public record BoudicaResponse(
        int StatusCode,
    string StatusDescription,
    IEnumerable<KeyValuePair<string, string>> Headers,
    byte[] BodyBytes)
{
    public static BoudicaResponse Create()
        => new BoudicaResponse(
            200, "OK",
            [],
            []);

    public BoudicaResponse WithStatus(int code, string desc)
        => this with { StatusCode = code, StatusDescription = desc };

    public BoudicaResponse WithHeader(string name, string value)
    {
        var newHeaders = new Dictionary<string, string>(this.Headers);
        newHeaders.Add(name, value);
        return this with { Headers = newHeaders };
    }

    public BoudicaResponse WithBody(string body)
        => this with { BodyBytes = Encoding.UTF8.GetBytes(body) };
}

public class BoudicaRequest
{
    public BoudicaRequest(string httpMethod, string resource, string httpVersion, Dictionary<string, string> headerDict, byte[]? requestBody)
    {
        HttpMethod = httpMethod;
        Resource = resource;
        HttpVersion = httpVersion;
        HeaderDict = headerDict;
        RequestBody = requestBody;
    }

    public string HttpMethod { get; }
    public string Resource { get; }
    public string HttpVersion { get; }
    public Dictionary<string, string> HeaderDict { get; }
    public byte[]? RequestBody { get; }
}