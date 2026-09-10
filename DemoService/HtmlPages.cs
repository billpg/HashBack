using DemoService.Controllers;
using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DemoService;

internal class HtmlPages
{
    internal static string Home()
        => GetHtmlByResource("DemoService.Docs.GetHome.md");

    internal static string HashRoot()
        => GetHtmlByResource("DemoService.Docs.GetHashRoot.md");

    internal static string HelloRoot()
        => GetHtmlByResource("DemoService.Docs.GetHelloRoot.md");

    internal static string CallRoot()
        => GetHtmlByResource("DemoService.Docs.GetCallRoot.md");

    internal static string Permit()
        => GetHtmlByResource("DemoService.Docs.GetPermit.md");

    private static string GetHtmlByResource(string resourceName)
    {
        /* Get the embedded stream and convert to HTML. If anything is missing a null
         * exception will fall, resulting in a 500 error. This is intentional. */
        var asm = typeof(HashController).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var md = reader.ReadToEnd();
        return MarkdownToHtml(md);
    }

    private static string MarkdownToHtml(string md)
    {
        /* Convert markdown to HTML, then pull out the various HTML elements. */
        var body = Markdown.ToHtml(md);
        var elements = XDocument.Parse("<html>" + body + "</html>").Root!.Elements().ToList();

        /* Load the template XML into memory. */
        var asm = typeof(HashController).Assembly;
        var templateStream = asm.GetManifestResourceStream("DemoService.Docs.SiteTemplate.xml");
        var htmlOut = XDocument.Load(templateStream!).Root!;

        /* Update the CSS link. */
        var linkStylesheet = htmlOut.Descendants().WhereElement("link").WhereAttribute("rel", "stylesheet").Single();
        linkStylesheet.SetAttributeValue("href", "https://www.hashback.dev/style.css");

        /* Update the hashback home link. */
        var homeLink = htmlOut.Descendants().WhereElement("a").WhereAttribute("href", ".").Single();
        homeLink.SetAttributeValue("href", "https://www.hashback.dev/");

        /* Update HashBack logo. */
        var imgBadge = htmlOut.Descendants().WhereElement("img").WhereClass("badge").Single();
        imgBadge.SetAttributeValue("src", "https://www.hashback.dev/HashBack-Badge-Logo.png");

        /* Loop through the lines of the converted markdown. */
        XElement currSection = htmlOut.Descendants().WhereElement("div").WhereClass("introduction").Single();
        foreach (var elem in elements)
        {
            if (elem.Name.LocalName == "h1")
            {
                /* Set both the H1 and TITLE elements. */
                var h1Out = htmlOut.Descendants().WhereElement("h1").Single();
                h1Out.Add(elem.Nodes());
                var titleOut = htmlOut.Descendants().WhereElement("title").Single();
                titleOut.Add(elem.Nodes());
            }
            else if (elem.Name.LocalName == "h2")
            {
                /* Create a new section with the class "panel" and the current header. */
                currSection = new XElement("section");
                currSection.SetAttributeValue("class", "panel");
                currSection.Add(elem);

                /* Find the footer and add the new section immediately before it. */
                htmlOut.Descendants().WhereElement("p").WhereClass("footer").Single().AddBeforeSelf(currSection);
            }
            else
            {
                /* If this is a <pre> block, set the style accordingly. */
                if (elem.Name.LocalName == "pre")
                    elem.SetAttributeValue("class", "preprecode");

                /* Add to the current section. */
                currSection.Add(elem);
            }
        }

        /* Complete HTML. */
        return "<!doctype html>\r\n" + htmlOut.ToString(SaveOptions.DisableFormatting);
    }



}

internal static class HtmlExtensions
{
    public static IEnumerable<XElement> WhereElement(this IEnumerable<XElement> elements, string elementName)
        => elements.Where(x => x.Name.LocalName == elementName);

    public static IEnumerable<XElement> WhereAttribute(this IEnumerable<XElement> elements, string attributeName, string attributeValue)
        => elements.Where(x => x.Attribute(attributeName)?.Value == attributeValue);

    /// <summary>
    /// Look for elements with a particular class on the list.
    /// </summary>
    /// <param name="elements">Elements to scan.</param>
    /// <param name="className">Class name sought.</param>
    /// <returns>Enumerable list of elements with this class.</returns>
    public static IEnumerable<XElement> WhereClass(this IEnumerable<XElement> elements, string className)
    {
        foreach (var element in elements)
        {
            /* If this element has no class attribute, move on. */
            var classAttr = element.Attribute("class")?.Value;
            if (classAttr == null)
                continue;

            /* Split the class attribute into single classes and
             * if the one we want is listed, return it. */
            if (classAttr.Split(' ').Contains(className))
                yield return element;
        }
    }
}
