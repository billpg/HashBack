using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Markdig;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using DemoService.Data;

namespace DemoService.Controllers;

[ApiController]
[Route("hash")]
public class HashController : ControllerBase
{
    private readonly IHashStore hashStore;

    public HashController(IHashStore hashStore)
    {
        this.hashStore = hashStore;
    }

    // GET /hash/
    [HttpGet]
    [Produces("text/html")]
    [SwaggerOperation(Summary = "HTML index for hash service", Description = "Returns an HTML page describing how to use the HashBack demo service.")]
    public ActionResult Get()
        => Content(HtmlPages.HashRoot(), "text/html", Encoding.UTF8);

    // GET /hash/{id}
    [HttpGet("{id:guid}")]
    [Produces("text/plain")]
    [SwaggerOperation(Summary = "Get stored hash", Description = "Returns the base64-encoded 32-byte hash previously stored for the given id if found.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Hash found", typeof(string))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No entry found for the given id")]
    public async Task<ActionResult> GetById(Guid id)
    {
        var requestHeaders = new StringBuilder();
        foreach (var h in Request.Headers)
            foreach (var sh in h.Value)
                requestHeaders.AppendLine($"{h.Key}: {sh}");

        var entry = await hashStore.TryGetHashAsync(id, Request.RequestIP(), requestHeaders.ToString());
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

        /* Store the hash. Refused if this id was used within the reuse-block window.
         * (AddedBy is deliberately IPAddress.None here, matching the original behaviour -
         * only GET requests to retrieve a hash log the requester's IP, not the upload.) */
        var added = await hashStore.TryAddHashAsync(id, hashAsBytes, IPAddress.None);

        /* Return success or otherwise. */
        if (added)
            return Content(Convert.ToBase64String(hashAsBytes), "text/plain");
        else
            return Conflict($"An entry with ID {id} already exists.");
    }
}
