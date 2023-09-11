using billpg.CrossRequestTokenExchange;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/* Locate and load README.md into memory. */
string readmePath = FindFileByName("README.md");
var readmeLines = File.ReadAllLines(readmePath).ToList();
var readmeOrigText = string.Join("\r\n", readmeLines);

/* Start a dictionary of keys and values. */
var keyValues = GenerateKeyValues("Main");

/* Loop through the entire README, looking for the JSON lines with the
 * selected keys that will need rewriting. */
foreach (int readmeLineIndex in Enumerable.Range(0, readmeLines.Count))
{
    /* Switch to a new set of values for each case study. */
    if (readmeLines[readmeLineIndex].Contains("Case Studies"))
        keyValues = GenerateKeyValues("CaseStudyApi");
    if (readmeLines[readmeLineIndex].Contains("# Webhooks"))
        keyValues = GenerateKeyValues("CaseStudyWebhooks");

    /* Split the current line by quote. If not exactly five parts, move on. */
    var currentLine = readmeLines[readmeLineIndex].Split('"');
    if (currentLine.Length != 5)
        continue;

    /* Is the key part of the JSON property a known key? */
    if (keyValues.TryGetValue(currentLine[1], out string? valueForKey) && valueForKey != null)
    {
        /* Swap in the new value and replace the reunited line in the file. */
        currentLine[3] = valueForKey;
        readmeLines[readmeLineIndex] = String.Join("\"", currentLine);
    }
}

/* If README has changed, rewrite back. */
if (readmeOrigText != string.Join("\r\n", readmeLines))
{
    Console.WriteLine("Saving modified README.md.");
    File.WriteAllLines(readmePath, readmeLines);
}

/* Announce end. */
Console.WriteLine("Finished helper/readme update.");

/* Find the file with the given name, starting from this file's folder moving upwards. */
string FindFileByName(string fileName)
{
    /* Start with this file's folder. */
    var folder = new FileInfo(thisFilePath()).Directory;
    string thisFilePath([CallerFilePath] string path = "")
        => path;

    /* Keep going until we find a file with the right name. */
    while (folder != null)
    {
        /* Is the file here? */
        var found = folder.GetFiles(fileName, SearchOption.AllDirectories).FirstOrDefault();
        if (found != null)
            return found.FullName;

        /* If not, move to the parent and try again. */
        folder = folder.Parent;
    }

    /* Couldn't find file anywhere. */
    throw new Exception("Could not find " + fileName);
}

/* Deterministically generate keys and values for the example JSON objects. */
Dictionary<string,string> GenerateKeyValues(string realm)
{
    /* Start an emopty collection. */
    var keyValues = new Dictionary<string, string>();

    /* Hash the realm to produce intiator and issuer keys. */
    string hmacKey = GenerateKeyFromString(realm + "HMAC");
    keyValues["HmacKey"] = hmacKey;

    /* Generate and sign bearer token. */
    string bearerToken = ToBearerToken(GenerateKeyFromString(realm + "token"));
    var tokenSignature = CryptoHelpers.SignBearerToken(hmacKey, bearerToken);
    keyValues["BearerToken"] = bearerToken;
    keyValues["BearerTokenSignature"] = tokenSignature;

    /* Completed collection. */
    return keyValues;
}

/* Create a random-looking string from a starting string. */
string GenerateKeyFromString(string v)
{
    /* Hash the input string and return in hex. */
    using var sha = System.Security.Cryptography.SHA256.Create();
    byte[] hash = sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(v));
    return Convert.ToBase64String(hash);
}

/* Convert a string to a digits-only example bearer token. */
string ToBearerToken(string k)
{
    var x = k.Where(c => c != '-').Select(c => c % 10);
    return "Token_" + string.Concat(x);

}