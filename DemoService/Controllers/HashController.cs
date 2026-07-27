using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Markdig;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;

namespace DemoService.Controllers;

[ApiController]
[Route("hash")]
public class HashController : ControllerBase
{
    private readonly ServiceData data;

    public HashController(ServiceData data)
    {
        this.data = data;
    }

    // GET /hash/
    [HttpGet]
    [Produces("text/html")]
    [SwaggerOperation(Summary = "HTML index for hash service", Description = "Returns an HTML page describing how to use the HashBack demo service.")]
    public ActionResult Get()
        => Content(
            GetHashRootHtml.Value
            .Replace("https://demo.hashback.example/", $"{Request.Scheme}://{Request.Host.Value}/"), 
            "text/html", Encoding.UTF8);
    private readonly Lazy<string> GetHashRootHtml = new Lazy<string>(GetHashRootHtmlInternal);
    private static string GetHashRootHtmlInternal()
    {
        /* Get the embedded stream and convert to HTML. If anything is missing a null
         * exception will fall, resulting in a 500 error. This is intentional. */
        var asm = typeof(HashController).Assembly;
        using var stream = asm.GetManifestResourceStream("DemoService.Docs.GetHashRoot.md");
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var md = reader.ReadToEnd();
        var body = Markdown.ToHtml(md);
        return $"<html>{body}</html>";
    }

    // GET /hash/{id}
    [HttpGet("{id:guid}")]
    [Produces("text/plain")]
    [SwaggerOperation(Summary = "Get stored hash", Description = "Returns the base64-encoded 32-byte hash previously stored for the given id if found.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Hash found", typeof(string))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No entry found for the given id")]
    public ActionResult GetById(Guid id)
    {
        var entry = data.TryGetHash(id, Request.RequestIP());
        if (entry == null)
            return NotFound();
        return Content(entry.HashAsString, "text/plain");
    }

    // PUT /hash/{id}
    [HttpPut("{id:guid}")]
    [Produces("text/plain")]
    [Consumes("text/plain")]
    [SwaggerOperation(Summary = "Store a hash", Description = "Store a base64-encoded 32-byte hash at the given id. Returns 200 OK on success, 400 on bad input, 409 if id exists.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Hash stored", typeof(string))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request body")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Entry already exists")]
    public async Task<ActionResult> Put(Guid id)
    {
        /* Load the request body as a plain text entry. */
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var newHash = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(newHash))
            return BadRequest("Request body must contain a non-empty plain text entry.");

        /* Parse the string after trimming and validate as right size. */
        byte[]? hashAsBytes = Helpers.TryParseBase64(newHash.Trim(), 256 / 8);
        if (hashAsBytes == null)
            return BadRequest($"Hash must be a valid base64-encoded block of {256/8} bytes.");

        /* Store the hash in the database. */
        var entry = new StoredHash(hashAsBytes, DateTime.UtcNow, IPAddress.None);
        var added = data.TryAddHash(id, entry);

        /* Return success or otherwise. */
        if (added)
            return Content(entry.HashAsString, "text/plain");
        else
            return Conflict($"An entry with ID {id} already exists.");
    }
}
