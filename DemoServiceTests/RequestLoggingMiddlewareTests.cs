using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class RequestLoggingMiddlewareTests
{
    /// <summary>A fake ILogger that captures the last formatted message, for asserting on
    /// what RequestLoggingMiddleware actually logged.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public string? LastMessage { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => LastMessage = formatter(state, exception);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [TestMethod]
    public async Task InvokeAsync_LogsTimestampCallerIpMethodAndUrl_ThenCallsNext()
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        bool nextCalled = false;
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/call/";
        context.Request.QueryString = new QueryString("?x=1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";

        var before = DateTime.UtcNow;
        await middleware.InvokeAsync(context);
        var after = DateTime.UtcNow;

        Assert.IsTrue(nextCalled, "Should still call the next middleware in the pipeline.");
        Assert.IsNotNull(logger.LastMessage);
        StringAssert.Contains(logger.LastMessage, "203.0.113.9", "Should log the caller IP via RequestIP().");
        StringAssert.Contains(logger.LastMessage, "POST", "Should log the HTTP method.");
        StringAssert.Contains(logger.LastMessage, "/call/?x=1", "Should log the request URL.");

        /* The logged timestamp should be a UTC time captured during the call - not, say,
         * the local zone, and not zero/default. */
        var loggedTimestamp = DateTime.Parse(logger.LastMessage!.Split(' ')[0]).ToUniversalTime();
        Assert.IsTrue(loggedTimestamp >= before.AddSeconds(-1) && loggedTimestamp <= after.AddSeconds(1),
            $"Logged timestamp {loggedTimestamp:O} should fall within the call, between {before:O} and {after:O}.");
    }
}
