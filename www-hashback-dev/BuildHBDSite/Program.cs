using System.Text;
using System.Xml.Linq;
using BuildHBDSite;

var projectFolder = Directory.GetCurrentDirectory();
var siteFolder = "/home/billdev/Develop/HashBack/www-hashback-dev/site";

/* Loaded as XDocument (not XElement) deliberately: new XDocument(XDocument) always deep-
 * clones, whereas new XDocument(XElement) only clones if that element already has a
 * parent - the first page built from a freshly-Parse()'d XElement would adopt it directly
 * instead, permanently mutating this shared template with that page's own content. */
var htmlTemplate = XDocument.Parse(File.ReadAllText(Path.Combine(projectFolder, "SiteTemplate.xml")));

/* The full set of pages, and the label each gets on every other page's nav button row.
 * "." (the home page) is keyed as such because that's the href SiteTemplate.xml's own
 * logo link already uses. */
var buttons = new Dictionary<string, string>
{
    { ".", "Home" },
    { "example.html", "Example Walk-Through" },
    { "acme.html", "Isn't that ACME?" },
    { "demo.html", "Play with the demo service." },
    { "rfc.html", "Becoming an RFC" },
    { "help.html", "Can you Help?" }
};

/* Page slug -> output filename. index.md is the one exception, since it both saves to
 * index.html and is the "." entry in the buttons row above. */
var pages = new Dictionary<string, string>
{
    { "index", "index.html" },
    { "example", "example.html" },
    { "acme", "acme.html" },
    { "demo", "demo.html" },
    { "rfc", "rfc.html" },
    { "help", "help.html" }
};

foreach (var (slug, outputFile) in pages)
{
    var markdownPath = Path.Combine(projectFolder, "Pages", $"{slug}.md");
    var markdownSource = File.ReadAllText(markdownPath);
    var selfHref = outputFile == "index.html" ? "." : outputFile;

    var html = SiteBuilder.BuildPage(htmlTemplate, markdownSource, buttons, selfHref);
    SaveHtml(html, outputFile);
}

void SaveHtml(XDocument doc, string filename)
{
    string html = "<!doctype html>\n" + doc.ToString();
    File.WriteAllBytes(Path.Combine(siteFolder, filename), Encoding.UTF8.GetBytes(html));
}
