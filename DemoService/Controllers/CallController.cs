using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using billpg.HashBackCore;
using System.Net;
using DemoService.Data;
using DemoService.Services;
using Microsoft.AspNetCore.Builder;
using billpg.SpartanHttpClient;

namespace DemoService.Controllers;

[ApiController]
[Route("call")]
public class CallController : ControllerBase
{
    /// <summary>
    /// Stored service data (configuration, etc).
    /// </summary>
    private readonly ServiceData data;

    /// <summary>
    /// The /hash store.
    /// </summary>
    private readonly IHashStore hashStore;

    /// <summary>
    /// HTTP Getter service. Refuses targets that haven't opted in via their own
    /// /.well-known/demo-hashback-dev.json file.
    /// </summary>
    private readonly IHttpGetter httpGetter;

    public CallController(ServiceData data, IHashStore hashStore, IHttpGetter httpGetter)
    {
        this.data = data;
        this.hashStore = hashStore;
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
        /* Pull out the request body as a string. This should be the target's URL. */
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var requestBody = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(requestBody))
            return BadRequest("Request body must contain non-empty plain text.");

        /* Trim, then validate the request body is a valid HTTPS URL.
         * Note that the HttpGetter service will make additional validations on the URL. */
        bool isValid = Uri.TryCreate(requestBody.Trim(), UriKind.Absolute, out var target);
        if (!isValid || target == null)
            return BadRequest("Request body must be a valid URL.");

        /* Build the Authorization header for the target's URL. */
        Guid id = Guid.NewGuid();
        var verifyUrl = new Uri($"https://{data.ConfigServiceHost}/hash/{id}");
        DateTime now = DateTime.UtcNow;
        var hashBackRequest = HashBackRequest.Create(target.Authority, now, verifyUrl);
        await hashStore.TryAddHashAsync(id, Convert.FromBase64String(hashBackRequest.VerificationHash), Request.RequestIP());

        /* Make a GET request to that URL. */
        var resp = await httpGetter.GetAsync(target, "HashBack " + hashBackRequest.AuthToken, Request.RequestIP(), OutboundGetSource.Call);

        /* Report to caller. */
        return Content(await BuildReport(target, id, resp), "text/plain");
    }

    private async Task<string> BuildReport(Uri target, Guid id, SpartanResponse resp)
    {
        /* Build a report of the response, including status code, headers, and body. */
        var report = new StringBuilder();
        report.AppendLine($"GET {target}:");
        report.AppendLine($"Status: {resp.StatusCode}");
        report.AppendLine($"Remote TLS Certificate (SHA-256, Base64): {resp.RemoteCertificateHash ?? "(none - not an HTTPS connection)"}");
        foreach (var header in resp.Headers)
            report.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        report.AppendLine("Body:");
        var body = resp.Body;
        report.AppendLine(body);
        report.AppendLine();
        var getHashEvents = await hashStore.ListGetHashEventsAsync(id);
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
