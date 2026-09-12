using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DemoService.Services;

public class IpRateLimitMiddleware
{
    // Configure these values as desired
    private const int DefaultMaxRequests = 60;                // max requests
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1); // time window

    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, SlidingWindow> _counters = new();

    public IpRateLimitMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Resolve client IP using existing helper extension.
        var ip = context.Request.RequestIP()?.ToString() ?? "unknown";

        var counter = _counters.GetOrAdd(ip, _ => new SlidingWindow(DefaultMaxRequests, DefaultWindow));

        if (!counter.TryRequest())
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            // Advise clients when they may retry (seconds). Using window length as a simple hint.
            context.Response.Headers["Retry-After"] = ((int)DefaultWindow.TotalSeconds).ToString();
            await context.Response.WriteAsync("Too many requests. Try again later.");
            return;
        }

        await _next(context);
    }

    // Simple sliding-window counter (thread-safe per-instance).
    private class SlidingWindow
    {
        private readonly int _max;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(int maxRequests, TimeSpan window)
        {
            _max = maxRequests;
            _window = window;
        }

        public bool TryRequest()
        {
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                // Purge old timestamps outside the sliding window
                while (_timestamps.Count > 0 && (now - _timestamps.Peek()) > _window)
                    _timestamps.Dequeue();

                if (_timestamps.Count >= _max)
                    return false;

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}