using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace DemoService.Services;

/// <summary>Logs every incoming request - UTC timestamp, caller IP, HTTP method, and URL -
/// via ILogger, so it lands wherever the rest of this service's logging does (console by
/// default). Placed first in the pipeline so it captures every request, including ones a
/// later middleware goes on to reject (429, an unhandled exception, and so on).</summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestLoggingMiddleware> logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogInformation("{Timestamp:O} {CallerIp} {Method} {Url}",
            DateTime.UtcNow,
            context.Request.RequestIP(),
            context.Request.Method,
            context.Request.GetEncodedPathAndQuery());

        await next(context);
    }
}
