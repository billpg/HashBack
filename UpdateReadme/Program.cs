using billpg.CrossRequestTokenExchange;

/* Announce start. */
Console.WriteLine("Cross Request Token Exchange.");

/* Set a flag indicating if we're regenerating the README. */
bool regnerateReadme = true;

/* Start a dictionary of keys and values. */
var keyValues = new Dictionary<string, string>();

/* Generate initiator and issuer keys. */
string initiatorsKey = CryptoHelpers.GenerateRandomKeyString();
string issuersKey = CryptoHelpers.GenerateRandomKeyString();
keyValues["InitiatorsKey"] = initiatorsKey;
keyValues["IssuersKey"] = issuersKey;

/* Turn the two keys into a HMAC key. */
var hmacKey = CryptoHelpers.CalculateHashKey(initiatorsKey, issuersKey);

/* Generate bearer token. */
string bearerToken = "This_is_an_impossible_to_guess_token";
keyValues["BearerToken"] = bearerToken;

/* Sign the bearer token. */
var tokenSignature = CryptoHelpers.SignBearerToken(hmacKey, bearerToken);
keyValues["BearerTokenSignature"] = tokenSignature;

/* Load the README.md file and update code samples. */
if (regnerateReadme)
{
    var readMeLines = File.ReadAllLines("../../../../README.md");
    for (int lineIndex = 0; lineIndex < readMeLines.Length; lineIndex++)
    {
        var currentLine = readMeLines[lineIndex].Split('"');
        if (currentLine.Length < 4 || currentLine[3] == "...")
            continue;

        if (keyValues.TryGetValue(currentLine[1], out string? valueForKey) && valueForKey != null)
        {
            currentLine[3] = valueForKey;
            readMeLines[lineIndex] = String.Join("\"", currentLine);
        }
    }

    /* Scan again, looking for the fixed salt bytes. */
    int saltLineIndex = FirstLineStartsWith(readMeLines, "- *Salt* ");
    string fixedSalt = System.Text.Encoding.ASCII.GetString(CryptoHelpers.FixedPbkdf2Salt.ToArray());
    readMeLines[saltLineIndex + 1] = " - \"" + fixedSalt.Substring(40*0, 40) + "\" +";
    readMeLines[saltLineIndex + 2] = " - \"" + fixedSalt.Substring(40*1, 40) + "\" +";
    readMeLines[saltLineIndex + 3] = " - \"" + fixedSalt.Substring(40*2, 40) + "\"";

    /* Find the test vector line. */
    int testVectorLineIndex = FirstLineStartsWith(readMeLines, "The following test strings");
    readMeLines[testVectorLineIndex + 1] = $"- Initiator's Key: \"{initiatorsKey}\"";
    readMeLines[testVectorLineIndex + 2] = $"- Issuer's Key: \"{issuersKey}\"";
    readMeLines[testVectorLineIndex + 3] = $"- HMAC Key (in hex): {BytesToHex(hmacKey)}";
    string testBearerToken = "Test-Bearer-Token";
    readMeLines[testVectorLineIndex + 4] = $"- Bearer Token: \"{testBearerToken}\"";
    var testSig = CryptoHelpers.SignBearerToken(hmacKey, testBearerToken);
    readMeLines[testVectorLineIndex + 5] = $"- HMAC Signature (in hex): {testSig}";

    /* Save the readme back with changes. */
    File.WriteAllLines("../../../../README.md", readMeLines);
}

int FirstLineStartsWith(IList<string> lines, string key)
{
    for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        if (lines[lineIndex].StartsWith(key))
            return lineIndex;
    return -1;
}

string BytesToHex(IList<byte> bytes)
    => string.Concat(bytes.Select(b => b.ToString("X2")));
