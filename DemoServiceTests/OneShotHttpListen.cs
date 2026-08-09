using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DemoServiceTests;

internal class OneShotHttpListen : IDisposable
{
    internal int ListenPort { get; private set; } = 0;
    private Action? listenerDispose = null;
    internal bool Called { get; private set; } = false;
    internal Uri? ReqUrl { get; private set; } = null;
    internal IDictionary<string,string>? ReqHeaders { get; private set; } = null;
    internal int RespondStatusCode { get; set; } = 200;
    internal string RespondBody { get; set; } = "";

    internal void Start()
    {
        HttpListener? listen = null;
        for (int tryPort = 9002; tryPort < 9999; tryPort++)
        {
            listen = new HttpListener();
            listen.Prefixes.Add($"http://localhost:{tryPort}/");
            try
            {
                listen.Start();
                this.ListenPort = tryPort;
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 32)
            { /* Ignore any try a new port. */ }
        }

        /* Save the dipose call for later. */
        this.listenerDispose = listen!.Close;

        /* Start the listen. */
        listen!.GetContextAsync().ContinueWith(MyHandler);
    }

    void MyHandler(Task<HttpListenerContext> task)
    {
        this.Called = true;
        this.ReqUrl = task.Result.Request.Url;

        var headers = new Dictionary<string, string>();
        foreach (var headerKey in task.Result.Request.Headers.AllKeys)
        {
            headers[headerKey!] = string.Join(",", task.Result.Request.Headers[headerKey]);
        }
        this.ReqHeaders = headers.AsReadOnly();

        task.Result.Response.StatusCode = this.RespondStatusCode;
        task.Result.Response.ContentType = "text/plain";
        task.Result.Response.OutputStream.Write(Encoding.ASCII.GetBytes(this.RespondBody));
        task.Result.Response.Close();
    }

    public void Dispose()
        => this.listenerDispose?.Invoke();
}
