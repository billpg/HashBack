using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using billpg.HashBackCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DemoService.Services;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(ex, "The response has already started, cannot write error response.");
                throw;
            }

            if (ex is AuthorizationParseException apex)
            {
                context.Response.StatusCode = 400;
                return;
            }

            _logger.LogError(ex, "Unhandled exception caught by ExceptionHandlingMiddleware.");
            await WriteProblemDetailsResponseAsync(context, ex).ConfigureAwait(false);
        }
    }

    private static int MapStatusCode(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException => StatusCodes.Status400BadRequest,
            FormatException => StatusCodes.Status400BadRequest,
            OverflowException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status409Conflict,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError,
        };
    }

    private static string MapTitle(Exception ex, int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status501NotImplemented => "Not Implemented",
            StatusCodes.Status500InternalServerError => "An unexpected error occurred",
            _ => "Error"
        };
    }

    private static async Task WriteProblemDetailsResponseAsync(HttpContext context, Exception ex)
    {
        int status = MapStatusCode(ex);
        var pd = new ProblemDetails
        {
            Status = status,
            Title = MapTitle(ex, status),
            Detail = ex.Message,
            Instance = context.Request.Path
        };

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(pd, options);
        await context.Response.WriteAsync(json).ConfigureAwait(false);
    }
}