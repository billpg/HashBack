using System.Text;
using System.Xml.Linq;
using System.Reflection;

var htmlTemplate = LoadTemplate("BuildHBDSite.SiteTemplate.xml");
var siteFolder = "/home/billdev/Develop/HashBack/www-hashback-dev/site";

var buttons = new Dictionary<string, string>
{
    { ".", "Home" },
    { "example.html", "Example Walk-Through" },
    { "acme.html", "Isn't that ACME?" },
    { "demo.html", "Play with the demo service." },
    { "help.html", "Can you Help?" }

};

// Index
{
    var html = new XDocument(htmlTemplate);

    html.Title().SetValue("HashBack | Server-to-Server authentication without secret storage");
    html.H1().SetValue("HashBack");
    html.Introduction().AddHtml(
        "Cloud servers shouldn’t need to hold onto long‑lived secrets like passwords, API tokens or private keys." +
        " <b>HashBack</b> removes that burden. Rather than storing credentials, your service can rely on the TLS" +
        " keys it already uses every day. With two short HTTPS transactions — one to declare who you are, and one" +
        " to prove it — both sides gain confidence without ever sharing or keeping a secret. It’s simple, tidy and" +
        " built on the infrastructure you already trust.");

    var cards = html.AddSection("cards");
    var card1 = cards.AddElement("article", "card");
    card1.AddHtml("<h3>Zero secret storage</h3>");
    card1.AddHtml(
        "<p>Remove the need to keep and manage cryptographic keys" +
        " or bearer tokens around for long periods of time.</p>");
    card1.AddHtml("<p>You've already invested in TLS. <b>Use it!</b></p>");

    var card2 = cards.AddElement("article", "card");
    card2.AddHtml("<h3>Two HTTPS transactions</h3>");
    card2.AddHtml(
        "<p>HashBack keeps the exchange short and friendly.</p>" +
        "<p>One call out, one call back.</p>");

    var card3 = cards.AddElement("article", "card");
    card3.AddHtml("<h3>Server-to-Server</h3>");
    card3.AddHtml("<p>Use it for general-purpose authentication between internet-facing services.</p>");

    var analogy = html.AddSection("panel");
    analogy.AddHtml("<h2>From a simple analogy to a practical authentication mechanism</h2>");
    analogy.AddHtml(
        "<p>HashBack takes inspiration from a phone-call analogy. The caller knows who they are calling, but the recipient doesn't know " +
        "who that call is coming from. <small>(\"1471\"? \"Star-69\"? What's that?)</small></p>");
    analogy.AddHtml("<p>But what if the recipient can call that original caller back! Now they can be" +
        " reassured that the caller actually was who they say they were.</p>");
    analogy.AddHtml(
        "<div class=\"gallery\">" +
        "<img src=\"PhoneCall-Frame-1.png\" alt=\"Hi Bob. I'm Alice!\" />" +
        "<img src=\"PhoneCall-Frame-2.png\" alt=\"Prove it.\" />" +
        "<img src=\"PhoneCall-Frame-3.png\" alt=\"You know my number. Call me back.\" />" +
        "<img src=\"PhoneCall-Frame-4.png\" alt=\"Hi Alice. Did you call me just now?\" />" +
        "</div>");
    analogy.AddHtml(
        "<p>Did you see what <b>didn't</b> happen? " +
        "<b>No-one</b> needed a cryptographic key or secret token.</p>");
    analogy.AddHtml(
        "<p>Now apply that idea to web authentication. The client knows, thanks to TLS, who they are " +
        " connecting to, but the server doesn't know who that incoming connection is from." +
        " If the server can call the client back, this time the server knows, thanks again to TLS," +
        " that the client is who they say they are. The two connections in opposite directions" +
        " complete the loop.</p>");

    html.AddSection("panel").AddButtons(buttons, ".", "example.html", "Next: Walking through an example session");

    SaveHtml(html, "index.html");
}

void SaveHtml(XDocument doc, string filename)
{
    string html = "<!doctype html>\r\n" + doc.ToString();
    html = html.Replace(((char)160).ToString(), "&nbsp;");
    File.WriteAllBytes(Path.Combine(siteFolder, filename), Encoding.UTF8.GetBytes(html));
}

// Example
{
    var html = new XDocument(htmlTemplate);

    html.Title().SetValue("HashBack | An Example Session");
    html.H1().SetValue("An example HashBack session");

    html.Introduction().AddHtml(
        "Let's follow one request from the client's first thought to the server's final nod." +
        " <i>Hashbert</i> the Hedgehog will annotate the important bits, because authentication is" +
        " easier to understand when someone points at the spiky details.");

    var step1 = html.AddStepSection("Declare the range of URLs you control");
    step1.AddHtml(
        "<p><b>Ahead of time</b>, the client administrator registers the exact narrow HTTPS URL or folder" +
        " where verification hashes will be published. The service uses this declaration to map the" +
        " URL back to the client and must reject URLs outside it. Avoid areas where the public might" +
        " be able to inject text such as comment forms.</p>");
    step1.AddCode(
        "My verification hashes will be published at:",
        "    https://client.example/api/hashback?id=*");
    step1.AddHashbert(
        "Choose a small patch of ground that you control. A single doorway is splendid, " +
        "but handing over the whole house is a bit much, even for a hedgehog with a clipboard.");

    var step2 = html.AddStepSection("Write the authentication request JSON");
    step2.AddHtml("<p>When you want to make <b>an authenticated request</b>, write a fresh JSON claim." +
        " This package of data will combine the version, destination host, current UTC time, a unique random value," +
        " and the URL where you're going to publish the hash of this JSON later.</p>");
    step2.AddCode(
        "{",
        "    \"Version\": \"BILLPG_DRAFT_4.2\",",
        "    \"Host\": \"server.example\",",
        "    \"Now\": 529297200,",
        "    \"Unus\": \"Rpgt4Fc5nMDq14LOps/hYQ==\",",
        "    \"Verify\": \"https://client.example/api/hashback?id=502542886\"",
        "}");
    step2.AddHtml(
        "<p>The <code><b>Unus</b></code> value prevents anyone from reusing your verification hash to impersonate you." +
        " Everything else in the JSON is predictable, but this field is fresh, random and known only to you and the" +
        " server you’re talking to.</p><p>Cryptographers would call it a <i>“nonce”</i>, but in England that word has… other" +
        " connotations. I’m hoping the community will adopt this alternative name instead. (<b>Unus</b> is Latin for <i>“Once”</i>.)</p>");

    step2.AddHashbert("This claim is fresh, specific, and meant for one destination." +
        " The <code>Unus</code> value must be unpredictable and unique, so a stale claim" +
        " cannot simply wander back in wearing a fake moustache.");

    var step3 = html.AddStepSection("Hash the JSON and publish the result");
    step3.AddHtml("<p>Run a <b>salted SHA-256 hash over your JSON</b> and base-64 the result. Publish that string" +
        " as a one-line text file at the URL you listed in your JSON earlier.</p>");
    step3.AddCode(
        "$ cat hashback-salt.bin auth.json \\",
        "    | sha256sum \\",
        "    | cut -d ' ' -f1 \\",
        "    | xxd -r -p \\",
        "    | base64 > verification-hash.txt",
        "$ scp verification-hash.txt user@host:/user/client.example/data/hashback/502542886.txt");
    step3.AddHtml(
        "<p>The salt is fixed and exists to make sure that the hashes are only useful" +
        " for this exchange. Because the salt is not sent over the wire, we avoid the" +
        " possibility of abusing public hashing services.</p>" +
        "<p>The salt itself, along with how it was derived, is on the GitHub site.</p>");
    step3.AddHashbert("The hash sits there, waiting to be retrieved, ready to exclaim <b>\"That Was Me!\"</b>.");

    var step4 = html.AddStepSection("Make the HTTP request");
    step4.AddHtml("<p>Encode the bytes of your JSON with base-64 and <b>add it to your request's headers</b> as" +
        " an <code>Authorization: Hashback</code> header.</p>");
    step4.AddHtml("<p>(You'd put the entire base-64 block as a single line without spaces." +
        " We've split it up on this example for clarity.)</p>");
    step4.AddCode(
        "POST /api/order HTTP/1.1",
        "Host: server.example",
        "Accept: application/json",
        "Authorization: HashBack eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMi",
        "                        IsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v",
        "                        dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYz",
        "                        VuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi",
        "                        aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaG",
        "                        FzaGJhY2s/aWQ9NTAyNTQyODg2In0=");
    step4.AddHashbert("The header carries the claim. The verification hash confirms the claim is real.");

    var step5 = html.AddStepSection("The server verifies the hash");
    step5.AddHtml("<p>Now the server has your JSON, they can now <b>check everything is right</b>.</p>");
    step5.AddCheckList(
        "The JSON is valid.",
        "The <code>Host</code> is correct.",
        "The <code>Now</code> timestamp is recent.",
        "The <code>Verify</code> URL belongs to a known user. (You.)",
        "The hash was retrieved over TLS with a known CA.");
    step5.AddHtml(
        "<p>With the supplied verification hash in hand, the server may now repeat the salted SHA-256 and" +
        " check it matches the expected verification hash. If they match, it must have come from you!</p>");
    step5.AddHtml(
        "<div class=\"flow\">" +
        "<div><strong>1. Decode</strong> Read the JSON claim from the header.</div>" +
        "<div><strong>2. Recalculate</strong> Hash the claim with the fixed salt.</div>" +
        "<div><strong>3. Compare</strong> Accept an exact match.</div>" +
        "</div>");
    step5.AddHashbert("A matching result confirms control of the registered website." +
        " The server has not been handed a long-lived secret but fresh proof from the right location." +
        " That is such a satisfactory conclusion it makes my spines wiggle.");

    html.AddSection("panel").AddButtons(buttons, "example.html", "acme.html", "Next: Wait! Isn't that ACME?");

    SaveHtml(html, "example.html");
}

// Isn't that ACME?
{
    var html = new XDocument(htmlTemplate);

    html.Title().SetValue("HashBack | Isn't that ACME?");
    html.H1().SetValue("Isn't that like ACME?");

    html.Introduction().AddHtml(
        "Yes, HashBack has a lot in common with ACME, in particular the \"call me back\" verification idea at its core." +
        " The important difference is the problem each of the two serves.");

    var compare = html.AddSection("panel");
    compare.AddHtml("<h2>Two related ideas, different jobs</h2>");
    compare.AddHtml("<p>ACME, the protocol behind <b>Let's Encrypt</b>, is designed to <i>establish</i> TLS" +
        " certificates. HashBack, in contrast, is simpler in operation because both sides <i>already have TLS</i> " +
        " working.</p>");
    compare.AddHtml(
        "<table class=\"comparison\">" +
        "<thead><tr><th>Question</th><th>ACME</th><th>HashBack</th></tr></thead>" +
        "<tbody>" +
        "<tr><td>Number of transactions needed</td><td>3</td><td>2</td></tr>" +
        "<tr><td>General-purpose API authentication</td><td class=\"no\">No</td><td class=\"yes\">Yes</td></tr>" +
        "<tr><td>Works without TLS already configured</td><td class=\"yes\">Yes</td><td class=\"no\">No</td></tr>" +
        "<tr><td>Useful for establishing TLS</td><td class=\"yes\">Yes</td><td class=\"no\">No</td></tr>" +
        "</tbody></table>");
    compare.AddHashbert(
        "ACME helps put the lock on the door. HashBack uses the lock." +
        " Both jobs matter, but they are not the same job. Also, I strongly recommend the lock.");

    var thanks = html.AddSection("panel");
    thanks.AddHtml("<h2>HashBack needs TLS first</h2>");
    thanks.AddHtml(
        "<p>HashBack relies on TLS to reassure the server that the verification hash came from the" +
        " site the client controls. Without valid TLS on both sides, the protocol cannot" +
        " provide its intended identity check.</p>");
    thanks.AddHtml(
        "<p>In that sense, HashBack only works because <b>ACME</b> and <b>Let's Encrypt</b> made it possible." +
        " HashBack only works because TLS protection is now widespread and automated," +
        " giving it the trustworthy channel it needs to build on.</p>");
    thanks.AddHtml("<p>Thank you ACME!</p>");
    thanks.AddHashbert("HashBack is just like ACME, only fewer coyotes are maimed.");

    html.AddSection("panel").AddButtons(buttons, "acme.html", "demo.html", "Next: Do you have a demo service?");

    SaveHtml(html, "acme.html");
}

// Demo
{
    var html = new XDocument(htmlTemplate);

    html.Title().SetValue("HashBack | Demo Service");
    html.H1().SetValue("Try the Demo Service");
    html.Introduction().AddHtml(
        "Experience HashBack authentication in action. Use our demo with your own code to see how the" +
        " exchange works. We'll even host your verification hashes while you're testing.");

    var hello = html.AddSection("panel");
    hello.AddHtml("<h2>Test Your Client Code</h2>");
    hello.AddHtml(
        "<div class=\"feature-item\">" +
        "<strong><code>https://demo.hashback.dev/hello/</code></strong>" +
        " Send a GET request to this URL and it'll respond with a cheery \"Hello\" message," +
        " but only if a valid HashBack authentication header is included in the request." +
        " (If you don't, it'll return full documentation for this service including some" +
        " sample code you can copy.)" +
        " Use this to test your client code will generate a " +
        " valid claim and will publish valid hashes." +
        "</div>");
    hello.AddHashbert("Is it me you're looking for?");

    var call = html.AddSection("panel");
    call.AddHtml("<h2>Test Your Service Code</h2>");
    call.AddHtml(
        "<div class=\"feature-item\">" +
        "<strong><code>https://demo.hashback.dev/call/</code></strong>" +
        " Send a POST request, including the URL of <i>your</i> service in the request body," +
        " and this demo service will make properly-formed HashBack authenticated GET request" +
        " to that URL. Once completed, the demo service will return a full log of the request" +
        " and response, including the requests when anyone tried to GET the verification hash." +
        "</div>");
    call.AddHashbert("You can point the \"call\" handler at the \"hello\" handler if you want." +
        " It's like shaking hands with yourself.");

    var hash = html.AddSection("panel");
    hash.AddHtml("<h2>We'll host your hashes!</h2>");
    hash.AddHtml(
        "<div class=\"feature-item\">" +
        "<strong><code>https://demo.hashback.dev/hash/</code></strong>" +
        " If you're developing your own HashBack client but you don't have a web server ready to" +
        " host your verification hashes yet, we've got your back. You can upload your hash text" +
        " on the demo server, ready for anyone to request it. For a minute or so. And it'll be " +
        " deleted after three GETs." +
        "</div>"        );
    hash.AddHashbert("Don't use that for anything important. Anyone can claim to be the demo" +
        " service. Even badgers!");

    var tryitnow = html.AddSection("panel");
    tryitnow.AddHtml("<h2>Try it now!</h2>");
    var goToDemoButton = tryitnow.AddElement("a", "btn btn-primary btn-big-center");
    goToDemoButton.SetAttributeValue("href", "https://demo.hashback.dev/");
    goToDemoButton.SetAttributeValue("target", "_blank");
    goToDemoButton.SetValue("Demo.HashBack.dev");
    html.AddSection("panel").AddButtons(buttons,  "demo.html", "help.html", "Next: Can you help?");
    SaveHtml(html, "demo.html");
}

// Help
{
    var html = new XDocument(htmlTemplate);

    html.Title().SetValue("HashBack | Can you help?");
    html.H1().SetValue("Can you help?");
    html.Introduction().AddHtml(
        "<b>HashBack</b> is still a work in progress. Can you help us get across the finishing line?");

    var security = html.AddSection("panel");
    security.AddHtml("<h2>Can you perform a security analysis?</h2>");
    security.AddHtml("<p>Do you know security and cryptography?</p>");
    var table = security.AddElement("table", "questions");
    table.SetAttributeValue("border", "0");
    var questions = new string[] {
        "Was switching from PBKDF2 to a single salted SHA‑256 the right call for this threat model?",
        "Is the two‑transaction model cryptographically sufficient, or does ACME’s"
        + " extra initiation step close an attack vector I haven’t considered?",
        "Should receiving services validate the entropy, uniqueness and freshness of the <code>Unus</code> value?",
        "Is using a fixed, public salt with SHA‑256 appropriate for this protocol’s goals?",
        "Are there any attack scenarios where an adversary could trick the server into fetching a malicious verification hash URL?",
        "Does the protocol adequately protect against replay attacks if the client publishes the same verification hash twice?",
        "Is the verification hash sufficiently bound to the intended destination host? Is the \"Host\" value in the JSON enough?",
        "Is it safe to assume that TLS identity guarantees are symmetric between client and server in all deployment environments?",
        "Are there any risks if the either side is behind a reverse proxy or CDN that terminates TLS?",
        "Is a fixed salt ever a liability?",
        "Should the salt be versioned or rotated over time?",
        "Should the JSON claim include an explicit expiration time or maximum validity window?",
        "Should the JSON claim include a client identifier beyond the verification URL?",
        "Should servers rate‑limit or throttle verification‑hash fetches to avoid abuse?"
        + " Could throttling enable a denial-of-service attack?",
        "Should clients delete verification hashes after successful authentication?",
        "Is this protocol simple enough to be formally modelled and would that be worthwhile?",
        "Are there any weaknesses or issues I haven’t considered?"
        };
    var questionMarks = "❓,❔,🤦‍,🤔,🤨,🕵️‍,🧙‍".Split(',');
    var rnd = new Random(84);
    for (int qIndex=0; qIndex < questions.Length; qIndex++) 
        table.AddHtml(
            $"<tr>" +
            $"<td valign=\"middle\" class=\"questionmark\">{questionMarks[rnd.Next(questionMarks.Length)]}{(char)160}</td>" +
            $"<td class=\"{((qIndex%2==0)?"questions-evenrow":"questions-oddrow")}\">{questions[qIndex]}</td>" +
            "</tr>");
    security.AddHtml("<p>If you've found any issues with my draft, please raise an issue on" +
        " <a href=\"https://github.com/billpg/HashBack\" target=\"_blank\">the project's GitHub</a>.</p>");
    security.AddHashbert("If anything looks suspicious, prod it gently. Preferably with a stick.");

    var coding = html.AddSection("panel");
    coding.AddHtml("<h2>Write a library</h2>");
    coding.AddHtml("<p>I've made a start with <a href=\"https://github.com/billpg/HashBack/tree/main/billpg.HashBackCore\">a" +
        " dot-net library</a> that implements the core of the exchange." +
        " I plan to extend this into a reusable module you can drop into any dot-net web service. Can you help the" +
        " project by implementing the exchange into other frameworks?</p>");
    coding.AddHashbert("I dream of a world where every language has a HashBack library and none of them segfault.");

    var money = html.AddSection("panel");
    money.AddHtml("<h2>Sponsor me! 💰💲🤑💲💰</h2>");
    money.AddHtml("<p>Would you like to support ongoing development?</p>" +
        "<p>The exact mechanism is still to be discussed — but if you're interested, please let me know.</p>");
    money.AddHashbert("If you help HashBack, you help me. And I am adorable.");

    html.AddSection("panel").AddButtons(buttons, "help.html", "https://github.com/billpg/HashBack/", 
        "Next: More technical docs at the Github!");
    SaveHtml(html, "help.html");

}


XDocument LoadTemplate(string name)
{
    string xml = LoadStringResource(name);
    return XDocument.Parse(xml);
}

string LoadStringResource(string name)
{   
    var asm = Assembly.GetExecutingAssembly();
    using var stream = asm.GetManifestResourceStream(name);
    using var reader = new StreamReader(stream!, Encoding.UTF8);
    var md = reader.ReadToEnd();
    return md;
}