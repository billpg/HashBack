/* This program updates the RFC draft XML for its examples,
 * ensuring the examples are consistent with the current state of the draft.
 * To run, use `dotnet run FixRfc.cs` from the rfc folder.
 *
 * The XML is edited as plain text, replacing only the content of the elements
 * marked with the anchors below, so that every other byte of the file (and so
 * the git diff) is left alone. Loading and re-saving it through an XML library
 * would also reformat the whole document. */
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/* Locate the highest version XML, ignoring any other files that happen to match. */
var xmlFile = Directory.GetFiles(Directory.GetCurrentDirectory(), "draft-godfrey-hashback-*.xml")
    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^draft-godfrey-hashback-\d+\.xml$"))
    .OrderByDescending(f => f)
    .First();

/* Load the current text, remembering whether it started with a byte-order mark. */
var xmlBytes = File.ReadAllBytes(xmlFile);
var hasBom = xmlBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble());
var xmlText = new UTF8Encoding(false).GetString(xmlBytes, hasBom ? 3 : 0, xmlBytes.Length - (hasBom ? 3 : 0));
var newLine = xmlText.Contains("\r\n") ? "\r\n" : "\n";

/* Replace the content of the one element whose opening tag carries the anchor, between
 * the two markers around it. Throws unless exactly one match is found. */
void ReplaceAnchored(string anchor, string before, string contentPattern, string after, string newContent)
{
    var pattern = new Regex(
        $"(<[^>]*\\banchor=\"{Regex.Escape(anchor)}\"[^>]*>(?:(?!<[^>]*\\banchor=)[\\s\\S])*?{before})({contentPattern})({after})");
    var matches = pattern.Matches(xmlText);
    if (matches.Count != 1)
        throw new InvalidOperationException($"Expected exactly one match for anchor \"{anchor}\", found {matches.Count}.");

    newContent = newContent.Replace("\r\n", "\n").Replace("\n", newLine);
    xmlText = pattern.Replace(xmlText, m => m.Groups[1].Value + newContent + m.Groups[3].Value);
}

/* Build a replacement JSON object. */
var claimJson = new JsonObject
{
    ["Version"] = "BILLPG_DRAFT_4.2",
    ["Host"] = "verifier.example",
    ["Now"] = 682718520,
    ["Unus"] = "Rpgt4Fc5nMDq14LOps/hYQ==",
    ["Verify"] = "https://prover.example/api/hashback?id=502542886"
};

/* The default encoder would escape characters such as "+", which would change the bytes hashed. */
var compactOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var indentedOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true,
    IndentSize = 4,
    NewLine = "\n"
};

/* Show the JSON indented for presentation. */
ReplaceAnchored("example-json", @"<!\[CDATA\[", @"[\s\S]*?", @"\]\]></sourcecode>",
    "\n" + claimJson.ToJsonString(indentedOptions) + "\n");

/* The bytes that are actually Base64-encoded and hashed are the compact form. */
var claimAsBytes = Encoding.UTF8.GetBytes(claimJson.ToJsonString(compactOptions));

/* Show the Base64 as an Authorization header, with line breaks for presentation. */
var claimAsBase64 = Convert.ToBase64String(claimAsBytes);
var claimAsLines = string.Concat(claimAsBase64.Chunk(68).Select(s => " " + new string(s) + "\n"));
ReplaceAnchored("example-auth-header", @"<!\[CDATA\[", @"[\s\S]*?", @"\]\]></sourcecode>",
    "\nAuthorization: HashBack\n" + claimAsLines);

/* Hash the JSON bytes with the fixed prefix. */
var hashInput = Convert.FromBase64String("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=").ToList();
hashInput.AddRange(claimAsBytes);
var hash = Convert.ToBase64String(SHA256.HashData(hashInput.ToArray()));
ReplaceAnchored("example-hash", "<tt>", @"[A-Za-z0-9+/]{43}=", "</tt>", hash);

/* Save only if something changed, keeping the original byte-order mark (or lack of one). */
var outBytes = new UTF8Encoding(hasBom).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(xmlText)).ToArray();
if (outBytes.AsSpan().SequenceEqual(xmlBytes))
{
    Console.WriteLine("Examples already up to date.");
}
else
{
    File.WriteAllBytes(xmlFile, outBytes);
    Console.WriteLine($"Updated the examples in {Path.GetFileName(xmlFile)}.");
}
