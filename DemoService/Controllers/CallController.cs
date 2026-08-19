using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using billpg.HashBackCore;
using System.Net;
using DemoService.Services;
using Microsoft.AspNetCore.Builder;

namespace DemoService.Controllers;

[ApiController]
[Route("call")]
public class CallController : ControllerBase
{
    /// <summary>
    /// Stored service data, including hashes and other state. 
    /// </summary>
    private readonly ServiceData data;

    /// <summary>
    /// HTTP Getter service.
    /// </summary>
    private readonly IHttpGetter httpGetter;

    public CallController(ServiceData data, IHttpGetter httpGetter)
    {
        this.data = data;
        this.httpGetter = httpGetter;
    }

    // GET /call/
    [HttpGet]
    [Produces("text/html")]
    [SwaggerOperation(Summary = "HTML index for call endpoint", 
        Description = "Returns an HTML page describing how to use the Call demo endpoint.")]
    public ActionResult Get()
        => Content(HtmlPages.CallRoot(), "text/html", Encoding.UTF8);

    // POST /call/
    [HttpPost]
    [Produces("text/plain")]
    [Consumes("text/plain")]
    [SwaggerOperation(Summary = "Submit a call requestBody", 
        Description = "Accepts a plain-text requestBody and returns a simple acknowledgement.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Payload received", typeof(string))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request body")]
    public async Task<ActionResult> Post()
    {
        /* Pull out the request body as a string. This should be the caller's URL. */
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var requestBody = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(requestBody))
            return BadRequest("Request body must contain non-empty plain text.");

        /* Trim, then validate the request body is a valid HTTPS URL.
         * Note that the HttpGetter service will make additional validations on the URL. */
        bool isValid = Uri.TryCreate(requestBody.Trim(), UriKind.Absolute, out var caller);
        if (!isValid || caller == null)
            return BadRequest("Request body must be a valid URL.");

        /* Build the Authorization header for the caller's URL. */
        Guid id = Guid.NewGuid();
        string verifyUrl = $"https://{data.ConfigServiceHost}/hash/{id}";
        DateTime now = DateTime.UtcNow;
        (string authHeader, string hash) = HashBackBuilder.Build(caller.Authority, now, verifyUrl);
        var storedHash = new StoredHash(Convert.FromBase64String(hash), now, Request.RequestIP());
        data.TryAddHash(id, storedHash);

        /* Make a GET request to that URL. */
        var req = new SimpleHttpRequest(caller)
            .WithHeader("Authorization", "HashBack " + authHeader);
        var resp = await httpGetter.GetAsync(req);

        /* Report to caller. */
        return Content(BuildReport(caller, id, resp), "text/plain");
    }

    private string BuildReport(Uri caller, Guid id, SimpleHttpResponse resp)
    {
        /* Build a report of the response, including status code, headers, and body. */
        var report = new StringBuilder();
        report.AppendLine($"GET {caller}:");
        report.AppendLine($"Status: {resp.StatusCode}");
        foreach (var header in resp.Headers)
            report.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        report.AppendLine("Body:");
        var body = resp.Body;
        report.AppendLine(body);
        report.AppendLine();
        var getHashEvents = data.ListGetHashEvents(id);
        report.AppendLine($"Logged GET /hash/{id} events: ({getHashEvents.Count})");
        for (int eventIndex = 0; eventIndex < getHashEvents.Count; eventIndex++)
        {
            var e = getHashEvents[eventIndex];
            report.AppendLine($"[{eventIndex + 1}/{getHashEvents.Count}] {e.GotAt} from {e.GotBy}");
            report.AppendLine(e.RequestHeaders);
        }
        return report.ToString();
    }
}