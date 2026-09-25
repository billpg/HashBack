using System.Xml.Linq;
using Markdig;

namespace BuildHBDSite;

/// <summary>A page's front matter - the "---" delimited block at the top of its markdown
/// source, giving the couple of things that can't come from the body itself (the browser
/// tab's title often differs from the on-page H1; the "next" nav button needs to know
/// where it's going before it can be built).</summary>
internal sealed record PageFrontMatter(string Title, string? NextHref, string? NextText);

/// <summary>
/// Turns one page's markdown source into a full HTML document, splicing it into the shared
/// SiteTemplate.xml the same way DemoService's own HtmlPages.MarkdownToHtml does: an H1
/// sets the page heading (title comes from front matter instead, since the two often
/// differ), each H2 starts a new ".panel" section, and everything else is appended to
/// whichever section is currently open.
///
/// The one addition beyond DemoService's version is the ":::{.step}" custom container
/// (see Markdig's CustomContainers + GenericAttributes extensions, both part of
/// UseAdvancedExtensions): a first-level H2 inside one becomes the section's title, wrapped
/// in the numbered "Step N" badge markup, with N counted automatically in document order.
/// Everything else that's specific to a single page - cards, the phone-call gallery, the
/// ACME comparison table, checklists, Hashbert's quotes - is just written as plain HTML
/// directly in the markdown source. CommonMark passes HTML blocks through untouched, and
/// (with a blank line on each side) still parses ordinary markdown formatting inside them -
/// see the Pages/*.md files for examples of both.
/// </summary>
internal static class SiteBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static XDocument BuildPage(XDocument template, string markdownSource, IReadOnlyDictionary<string, string> buttons, string selfHref)
    {
        var (frontMatter, body) = ParseFrontMatter(markdownSource);

        var doc = new XDocument(template);
        doc.Descendants("title").Single().SetValue(frontMatter.Title);

        var h1 = doc.Descendants("h1").Single();
        var footer = doc.Descendants("p").WhereClass("footer").Single();
        var currentSection = doc.Descendants("div").WhereClass("introduction").Single();
        int stepNumber = 0;

        var bodyHtml = Markdown.ToHtml(body, Pipeline);
        var bodyElements = XElement.Parse("<x>" + bodyHtml + "</x>").Elements().ToList();

        foreach (var elem in bodyElements)
        {
            if (elem.Name.LocalName == "h1")
            {
                h1.Add(elem.Nodes());
                continue;
            }

            if (elem.Name.LocalName == "h2")
            {
                currentSection = new XElement("section", new XAttribute("class", "panel"), elem);
                footer.AddBeforeSelf(currentSection);
                continue;
            }

            if (elem.Name.LocalName == "div" && elem.HasClass("step"))
            {
                footer.AddBeforeSelf(BuildStepArticle(elem, ++stepNumber));
                continue;
            }

            if (elem.Name.LocalName == "pre")
                UnwrapCodeBlock(elem);

            currentSection.Add(elem);
        }

        if (frontMatter.NextHref != null)
            footer.AddBeforeSelf(new XElement("section", new XAttribute("class", "panel"),
                BuildButtonRow(buttons, selfHref, frontMatter.NextHref, frontMatter.NextText!)));

        return doc;
    }

    /// <summary>Rebuilds a ":::{.step}" container's first H2 into the numbered
    /// step-header markup, keeping the rest of its content as-is.</summary>
    private static XElement BuildStepArticle(XElement stepContainer, int stepNumber)
    {
        var h2 = stepContainer.Element("h2");
        h2?.Remove();

        foreach (var pre in stepContainer.Descendants("pre").ToList())
            UnwrapCodeBlock(pre);

        var remainingChildren = stepContainer.Elements().ToList();

        var header = new XElement("div", new XAttribute("class", "step-header"),
            new XElement("span", new XAttribute("class", "number"), $"Step {stepNumber}"),
            h2 ?? new XElement("h2"));

        return new XElement("article", new XAttribute("class", "step"), header, remainingChildren);
    }

    /// <summary>
    /// Markdig renders a fenced code block as <pre><code>...</code></pre>. Flattens that
    /// down to a bare, class-tagged <pre>raw text</pre> - not just for CSS (.preprecode
    /// targets <pre> directly, no <code> needed), but because XDocument.ToString()'s
    /// default pretty-printer inserts its own indentation before any *child element* it
    /// finds, and <pre>'s one child here would be that <code> element. That corrupts every
    /// line's whitespace but the first, which is exactly the bug the nbsp hack this whole
    /// rewrite exists to remove was originally papering over. A <pre> with no child
    /// elements - just a single text node - gives the pretty-printer nothing to touch.
    /// </summary>
    private static void UnwrapCodeBlock(XElement pre)
    {
        var code = pre.Element("code");
        var text = code?.Value ?? pre.Value;
        pre.RemoveNodes();
        pre.SetAttributeValue("class", "preprecode");
        pre.Add(text);
    }

    private static XElement BuildButtonRow(IReadOnlyDictionary<string, string> buttons, string skip, string nextHref, string nextText)
    {
        var keys = buttons.Keys.ToHashSet();
        keys.Remove(skip);
        keys.Remove(nextHref);

        var row = new XElement("div", new XAttribute("class", "btn-row"),
            new XElement("a", new XAttribute("class", "btn btn-primary"), new XAttribute("href", nextHref), nextText));

        foreach (var key in keys)
            row.Add(new XElement("a", new XAttribute("class", "btn btn-secondary"), new XAttribute("href", key), buttons[key]));

        return row;
    }

    /// <summary>
    /// Parses the "---" delimited front matter block at the top of a page's markdown
    /// source. Each line is "key: value" - split only on the first ": ", so a value is free
    /// to contain its own colons (e.g. "next-text: Next: Wait! Isn't that ACME?") without
    /// needing any quoting.
    /// </summary>
    private static (PageFrontMatter frontMatter, string body) ParseFrontMatter(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            throw new InvalidOperationException("Page markdown must start with a '---' front matter block.");

        var values = new Dictionary<string, string>();
        int i = 1;
        for (; i < lines.Length && lines[i].Trim() != "---"; i++)
        {
            int colonSpace = lines[i].IndexOf(": ", StringComparison.Ordinal);
            if (colonSpace < 0)
                throw new InvalidOperationException($"Malformed front matter line (expected 'key: value'): '{lines[i]}'");
            values[lines[i][..colonSpace].Trim()] = lines[i][(colonSpace + 2)..].Trim();
        }
        if (i >= lines.Length)
            throw new InvalidOperationException("Front matter was never closed with a second '---' line.");

        var body = string.Join('\n', lines.Skip(i + 1));

        if (!values.TryGetValue("title", out var title))
            throw new InvalidOperationException("Front matter is missing the required 'title' field.");
        values.TryGetValue("next", out var next);
        values.TryGetValue("next-text", out var nextText);

        return (new PageFrontMatter(title, next, nextText), body);
    }
}

internal static class XmlQueryExtensions
{
    public static IEnumerable<XElement> WhereClass(this IEnumerable<XElement> elements, string className)
        => elements.Where(e => e.HasClass(className));

    public static bool HasClass(this XElement element, string className)
        => (element.Attribute("class")?.Value ?? "").Split(' ').Contains(className);
}
