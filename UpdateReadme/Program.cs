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
readmeLines[fixedSaltIndex] = "     - `" + fixedSalt + "`<!--FIXED_SALT-->";

/* Return the number of seconds since 1970 for the supplied timestamp. */
long UnixTime(DateTime utc)
{
    return (long)(utc.ToUniversalTime() - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds;
}

void PopulateExample(string keyBase, DateTime issuedAt, string issuerUrl, string verifyUrl)
{
    /* Build request JSON. */
    var requestJson = new JObject();
    requestJson["CrossRequestTokenExchange"] = "CRTE-PUBLIC-DRAFT-3";
    requestJson["IssuerUrl"] = issuerUrl;
    requestJson["Now"] = UnixTime(issuedAt);
    requestJson["Unus"] = GenerateUnus(keyBase);
    requestJson["Rounds"] = 1;
    requestJson["VerifyUrl"] = verifyUrl;
    ReplaceJson($"<!--{keyBase}_REQUEST-->", requestJson);

    /* Insert the hash of the above JSON into the readme. */
    string hash1066 = CryptoHelpers.HashRequestBody(requestJson);
    int hash1066Index = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_HASH-->"));
    readmeLines[hash1066Index] = $"- \"{hash1066}\"<!--{keyBase}_HASH-->";

    /* Build response JSON. */
    var responseJson = new JObject();
    responseJson["BearerToken"] = ToBearerToken(new Uri(verifyUrl).Host, new Uri(issuerUrl).Host, issuedAt, out DateTime expiresAt);
    responseJson["ExpiresAt"] = UnixTime(expiresAt);
    ReplaceJson($"<!--{keyBase}_RESPONSE-->", responseJson);
}

void ReplaceJson(string tag, JObject insert)
{ 
    /* Look for the "1066" example JSON. */
    int markerIndex = readmeLines.FindIndex(src => src.Contains(tag));
    int openBraceIndex = readmeLines.FindIndex(markerIndex, src => src == "{");
    int closeBraceIndex = readmeLines.FindIndex(openBraceIndex, src => src == "}");
    readmeLines.RemoveRange(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

    /* Insert back into code. */
    readmeLines.Insert(openBraceIndex, insert.ToString().Replace("\r\n  ", "\r\n    "));
}

PopulateExample(
    "1066_EXAMPLE", 
    DateTime.Parse("1986-10-09T23:00:00-04:00"), 
    "https://issuer.example/api/generate_bearer_token", 
    "https://caller.example/crte_files/C4C61859.txt");

PopulateExample(
    "CASE_STUDY",
    DateTime.Parse("2005-03-26T19:00:00Z"),
    "https://sass.example/api/login/crte",
    "https://carol.example/crte/64961859.txt");

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

/* Create a random-looking 256-bit Base64 encoded string from a starting string. */
string GenerateUnus(string v)
{
    /* Hash the input string and return in hex. */
    using var sha = System.Security.Cryptography.SHA256.Create();
    byte[] hash = sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(v + "Unus"));
    return Convert.ToBase64String(hash);
}

/* Convert a string into an example JWT. */
string ToBearerToken(string caller, string issuer, DateTime issuedAt, out DateTime expiresAt)
{
    expiresAt = issuedAt.AddDays(1).ToUniversalTime();
    long iat = As1970Seconds(issuedAt);
    long exp = As1970Seconds(expiresAt);

    string jwtHeader = JWT64Encode(new JObject { ["typ"] = "JWT", ["alg"] = "HS256", [""] = "billpg.com/nggyu" });
    string jwtBody = JWT64Encode(new JObject { ["sub"] = caller, ["iss"] = issuer, ["iat"] = iat, ["exp"] = exp });
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