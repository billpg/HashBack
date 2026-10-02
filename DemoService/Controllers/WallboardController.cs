using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        var md = BuildMarkdown(
            now, data.StartedAt,
            helloTotal, helloSuccess, hello24h, helloByOutcome, recent,
            outboundTotal, outboundBySourceAndHost,
            hashTotal, hashRetrieved);
        return Content(HtmlPages.FromMarkdown(md), "text/html", Encoding.UTF8);
    }

    private static string BuildMarkdown(
        DateTime now, DateTime startedAt,
        int helloTotal, int helloSuccess, int hello24h,
        IEnumerable<(string Outcome, int Count)> helloByOutcome, IEnumerable<HelloRequest> recent,
        int outboundTotal, IEnumerable<(string Source, string TargetHost, int Count)> outboundBySourceAndHost,
        int hashTotal, int hashRetrieved)
    {
        var uptime = now - startedAt;
        var successRate = helloTotal == 0 ? 0 : (100.0 * helloSuccess / helloTotal);

        var md = new StringBuilder();
        md.AppendLine("# 🦔 HashBack Wallboard");
        md.AppendLine();
        /* No arrow function (=>) here deliberately - HtmlPages' template pipeline
         * round-trips this HTML through XDocument, and .NET's XML writer always escapes
         * '>' in text content on the way back out, corrupting "=>" into "=&gt;" and
         * breaking the script. Plain function syntax has no '>' to mangle. */
        md.AppendLine("<script>setTimeout(function() { location.reload(); }, 15000);</script>");
        md.AppendLine();
        md.AppendLine($"- Refreshed {now:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"- Up since {startedAt:yyyy-MM-dd HH:mm:ss} UTC ({FormatUptime(uptime)})");
        md.AppendLine("- Auto-refreshes every 15s");
        md.AppendLine();

        md.AppendLine("## 📈 Hello Requests");
        md.AppendLine();
        md.AppendLine($"**{helloTotal}** total &middot; **{helloSuccess}** succeeded ({successRate:0.#}%) " +
            $"&middot; **{hello24h}** in the last 24h");
        md.AppendLine();
        md.AppendLine("<table><tr><th>Outcome</th><th>Count</th></tr>");
        foreach (var row in helloByOutcome)
            md.AppendLine($"<tr><td>{row.Outcome}</td><td>{row.Count}</td></tr>");
        md.AppendLine("</table>");
        md.AppendLine();

        md.AppendLine("## 🫱 Outbound GETs");
        md.AppendLine();
        md.AppendLine($"**{outboundTotal}** total");
        md.AppendLine();
        md.AppendLine("<table><tr><th>Source</th><th>Target Domain</th><th>Count</th></tr>");
        foreach (var row in outboundBySourceAndHost)
            md.AppendLine($"<tr><td>{row.Source}</td><td>{row.TargetHost}</td><td>{row.Count}</td></tr>");
        md.AppendLine("</table>");
        md.AppendLine();

        md.AppendLine("## 🔑 Hash Store");
        md.AppendLine();
        md.AppendLine($"**{hashTotal}** stored &middot; **{hashRetrieved}** retrieved at least once");
        md.AppendLine();

        md.AppendLine("## 🕒 Recent Hello Activity");
        md.AppendLine();
        md.AppendLine("<table><tr><th>Time (UTC)</th><th>Caller</th><th>Outcome</th></tr>");
        foreach (var r in recent)
            md.AppendLine($"<tr><td>{r.RequestedAt:yyyy-MM-dd HH:mm:ss}</td><td>{r.CallerIp}</td><td>{r.Outcome}</td></tr>");
        md.AppendLine("</table>");

        return md.ToString();
    }

    private static string FormatUptime(TimeSpan uptime)
        => uptime.TotalDays >= 1
            ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
            : uptime.TotalHours >= 1
                ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
                : $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
}
