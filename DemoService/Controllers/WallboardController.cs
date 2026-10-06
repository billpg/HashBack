using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using DemoService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace DemoService.Controllers;

/// <summary>
/// A simple, self-refreshing live stats page - deliberately homebrew (plain SQL counts
/// over the data already being logged by HelloRequestLog/OutboundGetLog/HashStore) rather
/// than a full metrics/observability stack, since this service's traffic is small enough
/// that a glance at a few totals is all anyone needs.
/// </summary>
[ApiController]
[Route("wallboard")]
public class WallboardController : ControllerBase
{
    private readonly HashDbContext db;
    private readonly ServiceData data;

    public WallboardController(HashDbContext db, ServiceData data)
    {
        this.db = db;
        this.data = data;
    }

    // GET /wallboard/
    [HttpGet]
    [Produces("text/html")]
    [SwaggerOperation(Summary = "Live stats wallboard",
        Description = "A self-refreshing page of simple counts drawn from the service's own request logs.")]
    public async Task<ActionResult> Get()
    {
        var now = DateTime.UtcNow;
        var since24h = now.AddHours(-24);

        var helloTotal = await db.HelloRequests.CountAsync();
        var helloSuccess = await db.HelloRequests.CountAsync(r => r.Outcome == HelloRequestOutcome.Success);
        var hello24h = await db.HelloRequests.CountAsync(r => r.RequestedAt >= since24h);
        var helloByOutcome = (await db.HelloRequests
            .GroupBy(r => r.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync())
            .Select(g => (g.Outcome.ToString(), g.Count));
        var recent = await db.HelloRequests
            .OrderByDescending(r => r.RequestedAt)
            .Take(10)
            .ToListAsync();

        var outboundTotal = await db.OutboundGets.CountAsync();
        var outboundBySourceAndHost = (await db.OutboundGets
            .GroupBy(g => new { g.Source, g.TargetHost })
            .Select(g => new { g.Key.Source, g.Key.TargetHost, Count = g.Count() })
            .ToListAsync())
            .OrderBy(g => g.Source)
            .ThenByDescending(g => g.Count)
            .Select(g => (g.Source.ToString(), g.TargetHost, g.Count));

        var hashTotal = await db.Hashes.CountAsync();
        var hashRetrieved = await db.Hashes.CountAsync(h => h.GetCount > 0);
        var hashesByCallerAndSource = (await db.Hashes
            .GroupBy(h => new { h.AddedBy, h.Source })
            .Select(g => new { g.Key.AddedBy, g.Key.Source, Count = g.Count() })
            .ToListAsync())
            .OrderBy(g => g.AddedBy.ToString())
            .ThenBy(g => g.Source)
            .Select(g => (g.AddedBy.ToString(), g.Source.ToString(), g.Count));

        var wallboardHtml = BuildWallboardHtml(
            now, data.StartedAt,
            helloTotal, helloSuccess, hello24h, helloByOutcome, recent,
            outboundTotal, outboundBySourceAndHost,
            hashTotal, hashRetrieved, hashesByCallerAndSource);
        return Content(wallboardHtml, "text/html", Encoding.UTF8);
    }

    private static string BuildWallboardHtml(
        DateTime now, DateTime startedAt,
        int helloTotal, int helloSuccess, int hello24h,
        IEnumerable<(string Outcome, int Count)> helloByOutcome, IEnumerable<HelloRequest> recent,
        int outboundTotal, IEnumerable<(string Source, string TargetHost, int Count)> outboundBySourceAndHost,
        int hashTotal, int hashRetrieved,
        IEnumerable<(string CallerIp, string Source, int Count)> hashesByCallerAndSource)
    {
        var uptime = now - startedAt;
        var successRate = helloTotal == 0 ? 0 : (100.0 * helloSuccess / helloTotal);

        var md = HtmlPages.GetResourceAsString("DemoService.Docs.WallboardTemplate.md");
        var html = HtmlPages.MarkdownToXElement(md);
        ReplaceElementText(html, "now", $"{now:yyyy-MM-dd HH:mm:ss}");
        ReplaceElementText(html, "startedAt", $"{startedAt:yyyy-MM-dd HH:mm:ss}");
        ReplaceElementText(html, "uptime", FormatUptime(uptime));
        ReplaceElementText(html, "helloTotal", helloTotal.ToString());
        ReplaceElementText(html, "helloSuccess", helloSuccess.ToString());
        ReplaceElementText(html, "successRate", $"{successRate:0.#}");       
        ReplaceElementText(html, "hello24h", hello24h.ToString());
        ReplaceElementLoop(html, "helloByOutcome", helloByOutcome, (elem, row) =>
        {
            ReplaceElementText(elem, "helloByOutcome.Outcome", row.Outcome);
            ReplaceElementText(elem, "helloByOutcome.Count", row.Count.ToString());
        });
        ReplaceElementText(html, "outboundTotal", outboundTotal.ToString());
        string? prevSource = null;
        ReplaceElementLoop(html, "outboundBySourceAndHost", outboundBySourceAndHost, (elem, row) =>
        {
            ReplaceElementText(elem, "outboundBySourceAndHost.Source", FirstOccurrenceOnly(row.Source, ref prevSource));
            ReplaceElementText(elem, "outboundBySourceAndHost.TargetHost", row.TargetHost);
            ReplaceElementText(elem, "outboundBySourceAndHost.Count", row.Count.ToString());
        });
        ReplaceElementText(html, "hashTotal", hashTotal.ToString());
        ReplaceElementText(html, "hashRetrieved", hashRetrieved.ToString());
        ReplaceElementLoop(html, "hashesByCallerAndSource", hashesByCallerAndSource, (elem, row) =>
        {
            /* Grouped by (AddedBy, Source), so a given caller IP appears on at most one
             * Put row - no need for FirstOccurrenceOnly's blanking here, unlike the
             * Outbound GETs table above, where the same Source genuinely does repeat
             * across different TargetHost rows. The template has no separate Source
             * column - it's folded into this cell instead, either the caller IP itself
             * (Put) or a "(via ...)" annotation (anything else). The annotation needs an
             * actual <i> element, not text containing "<i>" - ReplaceElementText's .Value
             * assignment would render that literally rather than as markup. */
            if (row.Source == "Put")
                ReplaceElementText(elem, "hashesByCallerAndSource.CallerIp", row.CallerIp);
            else
                ReplaceElementContent(elem, "hashesByCallerAndSource.CallerIp", new XElement("i", $"(via {row.Source})"));
            ReplaceElementText(elem, "hashesByCallerAndSource.Count", row.Count.ToString());
        });
        ReplaceElementLoop(html, "recent", recent, (elem, row) =>
        {
            ReplaceElementText(elem, "r.RequestedAt", $"{row.RequestedAt:yyyy-MM-dd HH:mm:ss}");
            ReplaceElementText(elem, "r.CallerIp", $"{row.CallerIp}");
            ReplaceElementText(elem, "r.Outcome", row.Outcome.ToString());
        });
        return HtmlPages.FinalizeHtml(html);
    }

    private static void ReplaceElementLoop<T>(XElement html, string repeatId, IEnumerable<T> items, Action<XElement, T> setItem)
    {
        var template = html.Descendants().Single(e => (string?)e.Attribute("id") == repeatId);
        template.Attribute("id")!.Remove();
        var parent = template.Parent!;
        template.Remove();
        foreach (var item in items)
        {
            var elem = new XElement(template);
            setItem(elem, item);
            parent.Add(elem);
        }
    }

    private static void ReplaceElementText(XElement html, string elementId, string newText)
    {
        var elem = html.Descendants().Single(e => (string?)e.Attribute("id") == elementId);
        elem.Value = newText;
        elem.Attribute("id")!.Remove();
    }

    /// <summary>Like ReplaceElementText, but for the rare case where the replacement is
    /// meant to be real markup (e.g. an &lt;i&gt; element) rather than literal text -
    /// ReplaceElementText's .Value assignment would render markup-looking text literally,
    /// which is the whole point of using it everywhere else. Only ever called with
    /// content built from a closed, fixed vocabulary (not arbitrary/external text), so
    /// there's nothing here that needs the same escaping ReplaceElementText provides.</summary>
    private static void ReplaceElementContent(XElement html, string elementId, params object[] content)
    {
        var elem = html.Descendants().Single(e => (string?)e.Attribute("id") == elementId);
        elem.RemoveNodes();
        elem.Add(content);
        elem.Attribute("id")!.Remove();
    }

    /// <summary>For a table whose rows are already sorted by some leading column (e.g.
    /// Source, or Caller IP) - gives that column a sectional feel by only naming each value
    /// on its first row, substituting a blank (non-breaking space, so the cell doesn't
    /// collapse) on every row that repeats it. The literal character, not the "&amp;#160;"
    /// entity - this goes straight into an XElement's .Value now rather than through
    /// Markdig, which is what used to decode the entity into this same character.</summary>
    private static string FirstOccurrenceOnly(string current, ref string? previous)
    {
        string display = current == previous ? " " : current;
        previous = current;
        return display;
    }

    private static string FormatUptime(TimeSpan uptime)
        => uptime.TotalDays >= 1
            ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
            : uptime.TotalHours >= 1
                ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
                : $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
}
