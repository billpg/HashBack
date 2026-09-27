using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int statusCode, string? detail)> InvokeAndReadProblemDetails(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware>? logger = null)
    {
        var middleware = new ExceptionHandlingMiddleware(next, logger ?? NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var detail = doc.RootElement.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : null;
        return (context.Response.StatusCode, detail);
    }

    [TestMethod]
    public async Task InvokeAsync_UnexpectedException_ReturnsGenericDetail_NotTheRawMessage()
    {
        /* A genuinely unmapped exception type - not one of the specific cases
         * MapStatusCode recognizes - falls through to a 500. Its message was never
         * written with a public caller in mind, so it should never reach one, even if
         * (as here) it happens to mention something Rutabaga Farms Inc. would rather
         * keep to itself, like a file path or connection string. */
        var secretDetail = "Could not open /home/rutabaga/secrets/connection-string.txt";
        var (statusCode, detail) = await InvokeAndReadProblemDetails(
            _ => throw new InvalidCastException(secretDetail));

        Assert.AreEqual(500, statusCode);
        Assert.AreNotEqual(secretDetail, detail, "The raw exception message must not reach the caller.");
        StringAssert.DoesNotMatch(detail, new System.Text.RegularExpressions.Regex("rutabaga", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [TestMethod]
    public async Task InvokeAsync_MappedClientException_StillReturnsItsOwnMessage()
    {
        /* Exception types MapStatusCode explicitly recognizes as 4xx/501 are a
         * deliberate, existing way of communicating something specific to the caller -
         * unlike the unmapped 500 case above, their message is meant to be public. */
        var (statusCode, detail) = await InvokeAndReadProblemDetails(
            _ => throw new ArgumentException("Rutabaga must be a positive weight."));

        Assert.AreEqual(400, statusCode);
        Assert.AreEqual("Rutabaga must be a positive weight.", detail);
    }
}
