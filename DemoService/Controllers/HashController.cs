using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

[ApiController]
[Route("hash")]
public class HashController : ControllerBase
{
    // In-memory store for demonstration. Thread-safe.
    private static readonly ConcurrentDictionary<Guid, StoredHashResult> _store
        = new ConcurrentDictionary<Guid, StoredHashResult>();

    // GET /hash/
    [HttpGet]
    public ActionResult<HashResult> Get()
    {
        var result = new HashResult();
        result.StoredHashes.AddRange(_store.Values);
        return Ok(result);
    }

    // GET /hash/{id}
    // Returns plain-text hash (text/plain)
    [HttpGet("{id:guid}")]
    [Produces("text/plain")]
    public ActionResult GetById(Guid id)
    {
        if (!_store.TryGetValue(id, out var entry))
            return NotFound();

        // Increment GetCount
        entry.GetCount++;

        // Return the stored hash as plain text
        return Content(entry.Hash, "text/plain");
    }

    // PUT /hash/{id}
    // Read raw request body (works regardless of input formatters)
    [HttpPut("{id:guid}")]
    [Produces("text/plain")]
    public async Task<ActionResult> Put(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var newHash = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(newHash))
            return BadRequest("Request body must contain a non-empty plain text hash.");
        
        var hashAsBytes = Encoding.UTF8.GetBytes(newHash.Trim());
        if (hashAsBytes.Length != 256/8)
            return BadRequest($"Hash must be exactly {256/8} bytes encoded as base64.");

        var hashRecord = new StoredHashResult
        {
            Id = id,
            Hash = Convert.ToBase64String(hashAsBytes),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = IPAddress.None.ToString(),
            GetCount = 0
        };
        if (!_store.TryAdd(id, hashRecord))
            return Conflict($"A hash with ID {id} already exists.");

        return Content(hashRecord.Hash, "text/plain");
    }
}

public class HashResult
{
    public List<StoredHashResult> StoredHashes { get; set; }
        = new List<StoredHashResult>();
}

public class StoredHashResult
{
    public Guid Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = IPAddress.None.ToString();
    public int GetCount { get; set; }
}

public class UpdateHashRequest
{
    public string Hash { get; set; } = string.Empty;
}