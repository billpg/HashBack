using billpg.CrossRequestTokenExchange;

/* Announce start. */
Console.WriteLine("Cross Request Token Exchange.");

/* Set a flag indicating if we're regenerating the README. */
bool regnerateReadme = false;

/* Start a dictionary of keys and values. */
var keyValues = new Dictionary<string, string>();

/* Generate initiator and issuer keys. */
string initiatorsKey = CryptoHelpers.GenerateRandomKeyString();
string issuersKey = CryptoHelpers.GenerateRandomKeyString();
keyValues["InitiatorsKey"] = initiatorsKey;
keyValues["IssuersKey"] = issuersKey;

/* Turn the two keys into a HMAC key. */
var hmacKey = CryptoHelpers.CalculateHashKey(initiatorsKey, issuersKey);

/* Hashes used in verify step. */
string intiatorsVerifyToken = CryptoHelpers.InitiatorsVerifyToken(hmacKey);
keyValues["IntiatorsVerifyToken"] = intiatorsVerifyToken;
string inssuersVerifyToken = CryptoHelpers.IssuersVerifyToken(hmacKey);
keyValues["InssuersVerifyToken"] = inssuersVerifyToken;

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
            currentLine[3] = valueForKey;

        readMeLines[lineIndex] = String.Join("\"", currentLine);
    }
    File.WriteAllLines("../../../../README.md", readMeLines);
}
