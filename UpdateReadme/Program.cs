using billpg.CrossRequestTokenExchange;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/* Generate the fixed salt in form of 120 capital letters. */
Console.WriteLine("Running GenFixedSalt.");
var sw = Stopwatch.StartNew();
var fixedSalt = GenFixedSalt.LoadFixedSaltFromCode();
sw.Stop();
Console.WriteLine($"Finished. Elapsed = {sw.ElapsedMilliseconds} ms.");

/* Load the lines of CryptoHelpers.cs into memory. */
string cryptoHelpersPath = FindFileByName("CryptoHelpers.cs");
var cryptoHelpersLines = File.ReadAllLines(cryptoHelpersPath).ToList();
var cryptoHelpersOrigText = string.Join("\r\n", cryptoHelpersLines);

/* Delete the lines between "FixedPbkdf2Salt" and "AsReadOnly". */
int insertTextIndex = ClearLinesBetween(cryptoHelpersLines, "FixedPbkdf2Salt", "AsReadOnly");

/* Insert the newly calculated fixed salt, chunking into 40 char blocks. */
const int saltChunkSize = 40;
var saltLinesForCryptoHelper =
    Enumerable.Range(0, fixedSalt.Length / saltChunkSize)
    .Select(i => $"            \"{fixedSalt.Substring(i*saltChunkSize, saltChunkSize)}\"");
saltLinesForCryptoHelper = AddSuffixExceptLast(saltLinesForCryptoHelper, " +");

/* Insert the lines into the code. */
cryptoHelpersLines.InsertRange(insertTextIndex, saltLinesForCryptoHelper);

/* Did it change the code? */
if (cryptoHelpersOrigText != string.Join("\r\n", cryptoHelpersLines))
{
    /* Announce modification of CryptoHelpers.cs and write out updated code. */
    Console.WriteLine("Writing modified CryptoHelpers.cs with new fixed salt.");
    File.WriteAllLines(cryptoHelpersPath, cryptoHelpersLines);

    /* Announce completion of this run. The rest of this program uses CryptoHelpers. */
    Console.WriteLine("Done. Please recompile before tunning again.");
    return;
}

/* Now locate and load README.md into memory. */
string readmePath = FindFileByName("README.md");
var readmeLines = File.ReadAllLines(readmePath).ToList();
var readmeOrigText = string.Join("\r\n", readmeLines);

/* Start a dictionary of keys and values. */
var keyValues = new Dictionary<string, string>();

/* Use fixed initiator and issuer strings to avoid un-needed changes. */
const string initiatorsKey = "THiDXXrxo2E-xLplG9j4ymi-QdxwMoMxUZi-D4B3vULbHyr";
const string issuersKey = "5Ml8wacaRTu-qGvw5KEKeHy-eGYtJAML4P9-nrWo7sLjpz0";
keyValues["InitiatorsKey"] = initiatorsKey;
keyValues["IssuersKey"] = issuersKey;

/* Turn the two keys into a HMAC key. */
var hmacKey = CryptoHelpers.CalculateHashKey(initiatorsKey, issuersKey);

/* Generate and sign bearer token. */
string bearerToken = "This_is_an_impossible_to_guess_token";
var tokenSignature = CryptoHelpers.SignBearerToken(hmacKey, bearerToken);
keyValues["BearerToken"] = bearerToken;
keyValues["BearerTokenSignature"] = tokenSignature;

/* Loop through the entire README, looking for the JSON lines with the
 * selected keys that will need rewriting. */
foreach (int readmeLineIndex in Enumerable.Range(0, readmeLines.Count))
{
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

/* Scan again, this time looking for the README's copy of the salt. */
int insertReadmeLineIndex = ClearLinesBetween(readmeLines, "- *Salt*", "- *Hash*");
var saltLinesForReadme = saltLinesForCryptoHelper.Select(s => $" - {s.Trim()}");
readmeLines.InsertRange(insertReadmeLineIndex, saltLinesForReadme);

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

/* Clear out the lines between two markers and return the line-insert index. */
int ClearLinesBetween(List<string> lines, string startMarker, string endMarker)
{
    /* Look for the starting and ending lines. */
    int startIndex = FindLineWhereContains(lines, 0, startMarker);
    int endIndex = FindLineWhereContains(lines, startIndex, endMarker);

    /* Remove those lines. */
    lines.RemoveRange(startIndex + 1, endIndex - startIndex - 1);

    /* Return the line after the start index, where lines should be inserted. */
    return startIndex + 1;
}

/* Find the index of the first line that contains the supplied key text. */
int FindLineWhereContains(List<string> lines, int startIndex, string marker)
{
    /* Loop through all line indexes. If it matches, return it. Otherwise throw error. */
    foreach (int i in Enumerable.Range(startIndex, lines.Count - startIndex))
        if (lines[i].Contains(marker))
            return i;
    throw new Exception("Can't find line with marker: " + marker);
}

/* Add a suffx to each line in the collection except the last. */
static IEnumerable<string> AddSuffixExceptLast(IEnumerable<string> lines, string suffix)
{
    /* Store the previous line as we're looping through the lines. */
    string? prev = null;

    /* Start looping through the lines. */
    foreach (string curr in lines)
    {
        /* Yield the previous line with the suffix added, unless this is the first time around. */
        if (prev != null)
            yield return prev + suffix;

        /* Move the current line into the previous. */
        prev = curr;
    }

    /* Ended loop. Return the final line (if there was one) without the suffic. */
    if (prev != null)
        yield return prev;
}
