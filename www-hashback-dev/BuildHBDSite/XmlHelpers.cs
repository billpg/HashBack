using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

internal static class XmlHelpers
{   
    public static XElement Title(this XDocument doc)
        => doc.FindElementName("title").Single();

    public static XElement H1(this XDocument doc)
        => doc.FindElementName("h1").Single();

    public static XElement Introduction(this XDocument doc)
        => doc.FindElementName("div").WhereClass("introduction").Single();

    public static XElement Main(this XDocument doc)
        => doc.FindElementName("main").Single();

    public static XElement AddSection(this XDocument doc, string className)
        => AddElementToMain(doc, "section", className);

    public static XElement AddElementToMain(this XDocument doc, string elementName, string className)
    {
        var main = doc.Main();
        var footer = main.Elements().WhereName("p").WhereClass("footer").Single();

        var section = new XElement("section");
        section.SetAttributeValue("class", className);
        footer.AddBeforeSelf(section);

        return section;
    }

    public static XElement AddStepSection(this XDocument doc, string title)
    {
        int lastStep = doc.Main()
            .Descendants()
            .WhereName("span")
            .WhereClass("number")
            .Select(x => x.Value)
            .Where(s => s.StartsWith("Step "))
            .Select(s => s.Split(' ').Last())
            .Select(int.Parse)
            .Append(0)
            .Max();

        var section = doc.AddElementToMain("article", "step");
        section.AddHtml(
            "<div class=\"step-header\">" +
            $"<span class=\"number\">Step {lastStep+1}</span>" +
            $"<h2>{title}</h2>" +
            "</div>");

        return section;
    }

    public static void AddCode(this XElement html, params string[] lines)
    {
        var div = html.AddElement("div", "preprecode");
        foreach (string line in lines)
            div.AddHtml($"<code>{line.Replace(' ', (char)160)}</code><br />");
    }

    public static void AddHashbert(this XElement html, string quote)
    {
        html.AddHtml($"<p class=\"hashbert\"><strong>🦔 Hashbert says:</strong> “{quote}”</p>");
    }

    public static void AddCheckList(this XElement html, params string[] items)
    {
        var table = html.AddElement("table", "checklist");
        table.SetAttributeValue("border", "0");
        foreach (var item in items)
            table.AddHtml($"<tr><td>{(char)160}✅{(char)160}</td><td>{item}</td></tr>");
    }

    public static IEnumerable<XElement> FindElementName(this XDocument doc, string elementName)
        => doc.Descendants().WhereName(elementName);

    public static IEnumerable<XElement> WhereName(this IEnumerable<XElement> items, string elementName)
        => items.Where(x => elementName == null || x.Name.LocalName == elementName);

    public static IEnumerable<XElement> WhereAttribute(this IEnumerable<XElement> items, string attrName, string attrValie)
        => items.Where(x => x.Attribute(attrName)?.Value == attrValie);

    public static IEnumerable<XElement> WhereClass(this IEnumerable<XElement> items, string className)
    {
        foreach (var item in items)
        {
            var classAttr = item.Attribute("class")?.Value;
            if (classAttr == null)
                continue;

            if (classAttr.Split(' ').Contains(className))
                yield return item;
        }
    }

    public static XElement AddElement(this XElement addTo, string elementName, string className)
    {
        var newElement = new XElement(elementName);
        newElement.SetAttributeValue("class", className);
        addTo.Add(newElement);
        return newElement;
    }

    public static void AddHtml(this XElement x, string html)
        => x.Add(XElement.Parse("<x>" + html + "</x>").Nodes());

    public static void AddButtons(this XElement x, Dictionary<string, string> buttons, string skip, string nextLink, string nextText)
    {
        var keys = buttons.Keys.ToHashSet();
        keys.Remove(skip);

        var buttonRow = x.AddElement("div", "btn-row");
        var nextButton = buttonRow.AddElement("a", "btn btn-primary");
        nextButton.SetAttributeValue("href", nextLink);
        nextButton.SetValue(nextText);
        keys.Remove(nextLink);

        foreach (string key in keys)
        {
            var secButton = buttonRow.AddElement("a", "btn btn-secondary");
            secButton.SetAttributeValue("href", key);
            secButton.SetValue(buttons[key]);
        }
    }

}

