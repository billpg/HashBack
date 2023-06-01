using billpg.BearerTokenExample;

/* Announce start. */
Console.WriteLine("TokenExchangeHelper demo.");

/* Generate the pre-flight dedication string. */
Console.WriteLine("Preflight Dedicatiom Text: " + PreFlight.DedicationText);

/* Set a flag indicating if we're regenerating the README. */
bool regnerateReadme = false;

/* Construct the helper, allowing both initiator and issuer to select keys. */
var helper = new TokenExchangeHelper(regnerateReadme ? "AKIZnpS4b+o-8wr0rYB42xa-30C+/yOLW0B-+kAWXDQ43ro" : null);
Console.WriteLine("InitiatorKeyText: " + helper.InitiatorKeyText);

/* Hashes used in verify step. */
Console.WriteLine("VerifyEvidenceForInitiator: " + helper.VerifyEvidenceForInitiator);
Console.WriteLine("VerifyEvidenceForIssuer: " + helper.VerifyEvidenceForIssuer);

/* Generate bearer token. */
var initiatorDomainName = "initiator.example";
Console.WriteLine("Initiator Domain: " + initiatorDomainName);
string bearerToken = "This_is_an_impossible_to_guess_Bearer_token_for_" + initiatorDomainName;
Console.WriteLine($"BearerToken: {bearerToken}");

/* Sign the bearer token. */
var bearerTokenSignature = helper.SignBearerToken(bearerToken);
Console.WriteLine($"TokenSignature: {bearerTokenSignature}");

/* Load the README.md file and update code samples. */
if (regnerateReadme)
{
    var readMeLines = File.ReadAllLines("../../../../README.md");
    for (int lineIndex = 0; lineIndex < readMeLines.Length; lineIndex++)
    {
        var currentLine = readMeLines[lineIndex].Split('"');
        if (currentLine.Length < 4 || currentLine[3] == "...")
            continue;
        if (currentLine[1] == "HmacKey")
            currentLine[3] = helper.InitiatorKeyText;
        if (currentLine[1] == "Dedication")
            currentLine[3] = PreFlight.DedicationText;
        if (currentLine[1] == "IssuerEvidence")
            currentLine[3] = helper.VerifyEvidenceForIssuer;
        if (currentLine[1] == "InitiatorEvidence")
            currentLine[3] = helper.VerifyEvidenceForInitiator;
        if (currentLine[1] == "BearerToken")
            currentLine[3] = bearerToken;
        if (currentLine[1] == "TokenHash")
            currentLine[3] = bearerTokenSignature;

        readMeLines[lineIndex] = String.Join("\"", currentLine);
    }
    File.WriteAllLines("../../../../README.md", readMeLines);
}
