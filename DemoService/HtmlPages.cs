using DemoService.Controllers;
using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoService;

internal class HtmlPages
{
    internal static string HashRoot()
        => GetHtmlByResource("DemoService.Docs.GetHashRoot.md");

    internal static string HelloRoot()
        => GetHtmlByResource("DemoService.Docs.HelloRoot.md");

    private static string GetHtmlByResource(string resourceName)
    {
        /* Get the embedded stream and convert to HTML. If anything is missing a null
         * exception will fall, resulting in a 500 error. This is intentional. */
        var asm = typeof(HashController).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var md = reader.ReadToEnd();
        var body = Markdown.ToHtml(md);
        return $"<html>{body}</html>";
    }

}
