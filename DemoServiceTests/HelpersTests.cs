using System.Xml.Linq;
using Markdig;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;

namespace DemoServiceTests;

[TestClass]
public class HelpersTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    /// <summary>Renders markdown to HTML and returns the text content of the first element
    /// found - i.e. what a reader would actually see, independent of exactly how many
    /// backticks the fence ended up using.</summary>
    private static string RenderedText(string markdown)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);
        return XElement.Parse("<x>" + html + "</x>").Value.TrimEnd('\n');
    }

    [TestMethod]
    [DataRow("Rutabaga")]
    [DataRow("Rutabaga`Farms")]
    [DataRow("Rutabaga``Farms")]
    [DataRow("`Rutabaga")]
    [DataRow("Rutabaga`")]
    [DataRow("`")]
    [DataRow("``")]
    public void MarkdownQuoteCode_RoundTripsThroughMarkdig_RendersOriginalText(string original)
    {
        var quoted = Helpers.MarkdownQuoteCode(original);
        Assert.AreEqual(original, RenderedText(quoted));
    }

    [TestMethod]
    public void MarkdownQuoteCode_EmptyString_RendersAsCodeSpanNotBrokenMarkdown()
    {
        /* An empty string is the one input this can't round-trip exactly: CommonMark only
         * trims a single leading/trailing space from a code span's content when that
         * content isn't *entirely* spaces, so the padding this needs (to stop the fence
         * merging with itself) survives as two literal spaces rather than disappearing.
         * That's an inherent limit of code-span syntax, not a bug - what actually matters
         * is that it still renders as one harmless code span, not broken/leaked markdown. */
        var quoted = Helpers.MarkdownQuoteCode("");
        var html = Markdown.ToHtml(quoted, Pipeline);
        StringAssert.Matches(html, new System.Text.RegularExpressions.Regex(@"^<p><code> *</code></p>\s*$"));
    }

    [TestMethod]
    public void MarkdownQuoteHeader_NormalHeader_RendersAsTwoCodeSpans()
    {
        var quoted = Helpers.MarkdownQuoteHeader("Host: server.example");
        Assert.AreEqual("Host: server.example", RenderedText(quoted));

        var html = Markdown.ToHtml(quoted, Pipeline);
        StringAssert.Contains(html, "<code>Host</code>");
        StringAssert.Contains(html, "<code>server.example</code>");
    }

    [TestMethod]
    public void MarkdownQuoteHeader_ValueContainsBacktick_StillRendersCorrectly()
    {
        var quoted = Helpers.MarkdownQuoteHeader("X-Rutabaga: a`b");
        Assert.AreEqual("X-Rutabaga: a`b", RenderedText(quoted));
    }

    [TestMethod]
    public void MarkdownQuoteHeader_NoColon_FallsBackToSingleCodeSpan()
    {
        var quoted = Helpers.MarkdownQuoteHeader("no-colon-here");
        Assert.AreEqual("no-colon-here", RenderedText(quoted));
    }

    [TestMethod]
    [DataRow("Cf-Connecting-Ip")]
    [DataRow("cf-ray")]
    [DataRow("CF-WARP-TAG-ID")]
    [DataRow("cf-")]
    public void IsCloudflareHeader_CfPrefixedHeaderAnyCase_ReturnsTrue(string headerName)
    {
        Assert.IsTrue(Helpers.IsCloudflareHeader(headerName));
    }

    [TestMethod]
    [DataRow("Host")]
    [DataRow("X-Forwarded-For")]
    [DataRow("Cdn-Loop")]
    [DataRow("Content-Type")]
    [DataRow("cf")]
    public void IsCloudflareHeader_NonCfHeader_ReturnsFalse(string headerName)
    {
        Assert.IsFalse(Helpers.IsCloudflareHeader(headerName));
    }
}
