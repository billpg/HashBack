/* Modify the README.md file with correct tokens and 
 * signatures using the crypto-helper functions. */
using billpg.CrossRequestTokenExchange;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/* Call the GenFixedSalt EXE to get the fixed salt. */
var psi = new ProcessStartInfo("./GenFixedSalt.exe");
psi.RedirectStandardOutput = true;
var fixedSalt = Process.Start(psi).StandardOutput.ReadLine();

/* Does it match the string in CryptoHelper? */
if (fixedSalt != CryptoHelpers.FIXED_SALT_AS_STRING)
{
    /* Rewrite CryptoHelpers.cs. */
    string cryptoHelperPath = FindFileByName("CryptoHelpers.cs");
    var cryptoLines = File.ReadAllLines(cryptoHelperPath).ToList();
    int sourceIndex = cryptoLines.FindIndex(src => src.Contains("FIXED_SALT_AS_STRING"))+1;
    var sourceLine = cryptoLines[sourceIndex].Split('"');
    cryptoLines[sourceIndex] = sourceLine[0] + "\"" + fixedSalt + "\";";
    File.WriteAllLines(cryptoHelperPath, cryptoLines);

    /* Report update. */
    Console.WriteLine("Updated CryptoHelper.cs. Rebuild and re-run.");
    return;
}

/* Locate and load README.md into memory. */
string readmePath = FindFileByName("README.md");
var readmeLines = File.ReadAllLines(readmePath).ToList();
var readmeOrigText = string.Join("\r\n", readmeLines);

/* Look for the first use of a JWT. */
int easterEggIndex = readmeLines.FindIndex(src => src.StartsWith("Authorization: Bearer ey"));
readmeLines[easterEggIndex] = "Authorization: Bearer " + GenerateEasterEggJWT();

/* Look for the fixed salt. */
int fixedSaltIndex = readmeLines.FindIndex(src => src.Contains("<!--FIXED_SALT-->"));
readmeLines[fixedSaltIndex] = "   - \"" + fixedSalt + "\"<!--FIXED_SALT-->";

/* Look for the "1066" example JSON. */
int index1066 = readmeLines.FindIndex(src => src.Contains("<!--1066_EXAMPLE_JSON-->"));
int startOf1066Index = readmeLines.FindIndex(index1066, src => src == "{");
int endOf1066Index = readmeLines.FindIndex(startOf1066Index, src => src == "}");
readmeLines.RemoveRange(startOf1066Index, endOf1066Index - startOf1066Index + 1);

/* Build request JSON. */
var requestJson = new JObject();
requestJson["CrossRequestTokenExchange"] = "CRTE-PUBLIC-DRAFT-3";
requestJson["IssuerUrl"] = "https://issuer.example/api/generate_bearer_token";
requestJson["Now"] = "1066-10-14T16:54:00Z";
requestJson["UniusUsusNumerus"] = GenerateKeyFromString("1066");
requestJson["VerifyUrl"] = "https://caller.example/crte_files/C4C61859.txt";

/* Insert back into code. */
readmeLines.Insert(startOf1066Index, requestJson.ToString());

/* Insert the hash of the above JSON into the readme. */
string hash1066 = CryptoHelpers.HashRequestBody(requestJson);
int hash1066Index = readmeLines.FindIndex(src => src.Contains("<!--1066_EXAMPLE_HASH-->"));
readmeLines[hash1066Index] = "- \"" + hash1066 + "\"<!--1066_EXAMPLE_HASH-->";

#if false
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

    /* Is this an Authorization header? */
    if (readmeLines[readmeLineIndex].StartsWith("Authorization: Bearer "))
    {
        if (readmeLines[readmeLineIndex].EndsWith("nggyu"))
            continue;
        readmeLines[readmeLineIndex] = "Authorization: Bearer " + keyValues["BearerToken"];
    }

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
#endif

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
    /* Start an empty collection. */
    var keyValues = new Dictionary<string, string>();

    /* Hash the realm to produce intiator and issuer keys. */
    string hmacKey = GenerateKeyFromString(realm + "HMAC");
    keyValues["HmacKey"] = hmacKey;

    /*
        "CrossRequestTokenExchange": "CRTE-PUBLIC-DRAFT-3",
        "IssuerUrl": "https://issuer.example/api/generate_bearer_token",
        "Now": "1066-10-14T16:54:00Z",
        "UniusUsusNumerus": "TODO - BASE64 256 random bits",
        "VerifyUrl": "https://caller.example/crte_files/C4C61859.txt" 
     */

    /* Generate and sign bearer token. */
    string bearerToken = ToBearerToken(realm, out DateTime expiresAt);
    keyValues["UniusUsusNumerus"] = GenerateKeyFromString(hmacKey);
    keyValues["ExpiresAt"] = expiresAt.ToString("s") + "Z";

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

/* Convert a string into an example JWT. */
string ToBearerToken(string realm, out DateTime expiresAt)
{
    string sub = 
        realm == "Main" ? "the_initiator" :
        realm == "CaseStudyApi" ? "12" :
        realm == "CaseStudyWebhooks" ? "saas" :
        throw new ApplicationException();
    string iss =
        realm == "Main" ? "the_issuer" :
        realm == "CaseStudyApi" ? "saas.example" :
        realm == "CaseStudyWebhooks" ? "carol.example" :
        throw new ApplicationException();
    int addSeconds =
        realm == "Main" ? 0 :
        realm == "CaseStudyApi" ? 20000000 :
        realm == "CaseStudyWebhooks" ? 20010000 :
        throw new ApplicationException();
    expiresAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddSeconds(addSeconds).ToUniversalTime();
    long iat = As1970Seconds(expiresAt);

    string jwtHeader = JWT64Encode(new JObject { ["typ"] = "JWT", ["alg"] = "HS256" });
    string jwtBody = JWT64Encode(new JObject { ["sub"] = sub, ["iss"] = iss, ["iat"] = iat });
    string jwtHeaderDotBody = jwtHeader + "." + jwtBody;

    using var hmac = System.Security.Cryptography.HMAC.Create("HMACSHA256");
    hmac.Key = System.Text.Encoding.ASCII.GetBytes("your-256-bit-secret");
    var hash = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes(jwtHeaderDotBody));
    string jwtSig = Convert.ToBase64String(hash).Replace('+','-').Replace('/','_').TrimEnd('=');
    string jwtFull = jwtHeaderDotBody + "." + jwtSig;
    return jwtFull;
}

long As1970Seconds(DateTime dt)
    => (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

string JWT64Encode(JObject j)
{
    string jwtAsJson = j.ToString(Newtonsoft.Json.Formatting.None);
    var jwtAsBytes = System.Text.Encoding.ASCII.GetBytes(jwtAsJson);
    var jwtAs64 = Convert.ToBase64String(jwtAsBytes);
    return jwtAs64.TrimEnd('=');
}

/* Generate a fake JWT with an easter egg. */
string GenerateEasterEggJWT()
{
    string Encode(string s)
    {
        s = s.Replace('\'', '\"');
        s = s.Replace('[', '{');
        s = s.Replace(']', '}');

        string base64 = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(s)).TrimEnd('=');
        return base64;
    }

    return Encode("['':'']") + "." + Encode($"['nggyu':'https://billpg.com/nggyu']") + ".nggyu";

}