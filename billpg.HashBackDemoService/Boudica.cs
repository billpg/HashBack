using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    public static Boudica Start(int port, bool useTls, RequestHandler handler)
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
                /* Remove all completed tasks from the open collection. */
                foreach (var openTask in openTasks)
                {
                    if (!openTask.IsCompleted)
                        continue;
                    await openTask;
                    openTask.Dispose();
                    openTasks.Remove(openTask);
                }

                /* Wait for a connection. */
                var tcp = await listen.AcceptTcpClientAsync(cancel.Token);
                if (tcp == null)
                    continue;

                /* Pass control to the handler as a task and store. */
                var handlerTask = HandleConnection(tcp, useTls, handler);
                openTasks.Add(handlerTask);               
            }

            /* Wait for all the open tasks to end. */
            Task.WaitAll(openTasks);
            listen.Stop();
        }
    }

    private static async Task HandleConnection(
        TcpClient tcp, bool useTls, RequestHandler handler)
    {
        using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream);

        var banner = await reader.ReadLineAsync();

    }

    public async Task Stop()
    {
        this.cancel.Cancel();
        await serviceTask;
        serviceTask.Dispose();
    }
}

public class BoudicaResponse
{
}

public class BoudicaRequest
{
}