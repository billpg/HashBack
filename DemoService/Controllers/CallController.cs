using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using billpg.HashBackCore;
using System.Net;
using DemoService.Services;

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

    /// <summary>
    /// Represents the builder used to construct hash-based data structures.
    /// </summary>
    private readonly HashBackBuilder<CallSession> builder;

    private record CallSession(string ServiceHost, Guid Id, Uri CallerUrl, DateTime Now, IPAddress Caller)
    {
        public string VerifyUrl => $"http://{ServiceHost}/hash/{Id}";
    }

    public CallController(ServiceData data, IHttpGetter httpGetter)
    {
        /* Store the data object for later use.
         * This is the persistent service data that will be shared across requests. */
        this.data = data;
        this.httpGetter = httpGetter;

        /* Initialize the HashBackBuilder with the necessary callbacks for 
         * generating verification URLs and registering hashes. */
        this.builder = new()
        {
            VerifyGetter = GenerateVerifyUrl,
            HashRegister = RegisterHash
        };
    }

    /// <summary>
    /// Build a verification URL that will be passed to the remote service in the form of
    /// an Authorization header. The remote service will call this URL to verify the hash.
    /// </summary>
    /// <returns>Verification URL with a unique ID in the place that RegsiterHash will expect it.</returns>
    private static Task<string> GenerateVerifyUrl(CallSession session)
        => Task.FromResult(session.VerifyUrl);

    /// <summary>
    /// Called by the HashBackBuilder class when it has a verification
    ///  URL and a corresponding verification hash to serve.
    /// </summary>
    /// <param name="session">Caller's session data.</param>
    /// <param name="_">Ignored verification URL. (Uses ID in session.)</param>
    /// <param name="hash">The veirifcation hash to return.</param>
    /// <returns>Async task.</returns>
    private Task RegisterHash(CallSession session, string _, string hash)
    {
        var storedHash = new StoredHash(
            Convert.FromBase64String(hash), session.Now, session.Caller);
        data.TryAddHash(session.Id, storedHash);
        return Task.CompletedTask;
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

        /* Trim, then validate the request body is a valid HTTPS URL. */
        bool isValid = Uri.TryCreate(requestBody.Trim(), UriKind.Absolute, out var caller);
        if (!isValid || caller == null || !IsSecure(caller))
            return BadRequest("Request body must be a valid HTTPS URL.");

        /* Build the Authorization header for the caller's URL. */
        var session = new CallSession(data.ConfigServiceHost, Guid.NewGuid(), caller!, DateTime.UtcNow, Request.RequestIP());
        string authHeader = await this.builder.BuildWithHost(session, caller.Authority);

        /* Make a GET request to that URL. */
        var resp = await MakeGetRequestToCaller(caller, authHeader);

        /* Build a report of the response, including status code, headers, and body. */
        var report = new StringBuilder();
        report.AppendLine($"GET {caller}:");
        report.AppendLine($"Status: {(int)resp.StatusCode}");
        foreach (var header in resp.Headers)
            report.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        report.AppendLine("Body:");
        var body = resp.Body;
        report.AppendLine(body);
        report.AppendLine();
        var getHashEvents = data.ListGetHashEvents(session.Id);
        report.AppendLine($"Logged GET /hash/{session.Id} events: ({getHashEvents.Count})");
        for (int eventIndex = 0; eventIndex < getHashEvents.Count; eventIndex++)
        {
            var e = getHashEvents[eventIndex];
            report.AppendLine($"[{eventIndex + 1}/{getHashEvents.Count}] {e.GotAt} from {e.GotBy}");
            report.AppendLine(e.RequestHeaders);
        }

        /* Return report to caller as plain text. */
        var reportAsString = report.ToString();
        return Content(reportAsString, "text/plain");
    }

    private async Task<SimpleHttpResponse> MakeGetRequestToCaller(Uri caller, string authHeader)
    {
        var req = new SimpleHttpRequest(caller)
            .WithHeader("Authorization", "HashBack " + authHeader);
            var resp = await httpGetter.GetAsync(req);
        return resp;
    }

    private bool IsSecure(Uri url)
    {
        /* Allow HTTP for localhost only, and only when configured as running as localhost. */
        if (url.Scheme == Uri.UriSchemeHttp &&
            url.Host == "localhost" &&
            url.Authority == data.ConfigServiceHost)
            return true;

        /* Reject anything other than HTTPS. */
        if (url.Scheme != Uri.UriSchemeHttps)
            return false;

        /* If the port is anything other than 443, reject it. */
        if (url.Port != 443)
            return false;

        /* If the host is less than five charcters, reject it. */
        if (url.Host.Length < 5)
            return false;

        /* If the URL contains any non-ascii characters, reject it. */
        if (url.Host.Any(c => c > 127) || url.PathAndQuery.Contains('%'))
            return false;

        /* If the host is an IP address, reject it. */
        if (IPAddress.TryParse(url.Host, out _))
            return false;

        /* If the host starts or ends with a dot, reject it. */
        if (url.Host.StartsWith('.') || url.Host.EndsWith('.'))
            return false;

        /* If the host is a single undotted string, reject it. */
        if (!url.Host.Contains('.'))
            return false;

        /* Anything else is considered secure. */
        return true;
    }
}