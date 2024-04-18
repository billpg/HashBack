/* Modify the README.md file with correct tokens and 
 * signatures using the crypto-helper functions. */
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

/* Locate and load README.md into memory. */
string readmePath = FindFileByName("README.md");
var readmeLines = File.ReadAllLines(readmePath).ToList();
var readmeOrigText = string.Join("\r\n", readmeLines);

/* Load the fixed salt bytes. */
const string fixed_salt_password = "To my Treacle.";
const string fixed_salt_salt = "I love you to the moon and back.";
const int fixed_salt_rounds = 238854 * 2; 
var fixedSaltBytes = Rfc2898DeriveBytes.Pbkdf2(
    password: Encoding.ASCII.GetBytes(fixed_salt_password),
    salt: Encoding.ASCII.GetBytes(fixed_salt_salt),
    iterations: fixed_salt_rounds,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 32);

string StringSaltParameters(string label, string value)
{
    int byteCount = value.Length;
    int byteSum = value.Select(ch => (int)ch).Sum();
    return $"- {label}: \"{value}\" ({byteCount} bytes, summing to {byteSum}.)";
}

/* Look for the fixed salt in the README. */
SetTextByMarker(readmeLines, "<!--FIXED_SALT_PASSWORD-->", StringSaltParameters("Password", fixed_salt_password));
SetTextByMarker(readmeLines, "<!--FIXED_SALT_DEDICATION-->", StringSaltParameters("Salt", fixed_salt_salt));
SetTextByMarker(readmeLines, "<!--FIXED_SALT_ITERATIONS-->", $"- Iterations: {fixed_salt_rounds}");

/* Look for the fixed salt byte block, two lines after the marker. */
int fixedSaltIndex = readmeLines.FindIndex(src => src.Contains("<!--FIXED_SALT-->")) + 2;
readmeLines.RemoveRange(fixedSaltIndex, 4);
readmeLines.Insert(fixedSaltIndex, DumpByteArray(fixedSaltBytes));

/* Look for the line with the fixed salt in hex/base64. */
SetTextByMarker(readmeLines, "<!--FIXED_SALT_HEX-->", $"- Hex: `{BytesToHex(fixedSaltBytes)}`");
SetTextByMarker(readmeLines, "<!--FIXED_SALT_B64-->", $"- Base64: `{Convert.ToBase64String(fixedSaltBytes)}`");
SetTextByMarker(readmeLines, "<!--FIXED_SALT_URL-->", $"- URL: `{System.Web.HttpUtility.UrlEncode(fixedSaltBytes)}`");

/* Populate the main examples in the README. */
PopulateExample(
    "1066_EXAMPLE",
    DateTime.Parse("1986-10-09T23:00:00-04:00"),
    "server.example",
    "https://client.example/hashback_files/my_json_hash.txt");

PopulateExample(
    "CASE_STUDY",
    DateTime.Parse("2005-03-26T19:00:00Z"),
    "rutabaga.example",
    "https://carol.example/hashback/64961859.txt");

/* If README has changed, rewrite back. */
if (readmeOrigText != string.Join("\r\n", readmeLines))
{
    Console.WriteLine("Saving modified README.md.");
    File.WriteAllLines(readmePath, readmeLines);
}

/* Announce end. */
Console.WriteLine("Finished helper/readme update.");

/* Return the number of seconds since 1970 for the supplied timestamp. */
long UnixTime(DateTime utc)
{
    return (long)(utc.ToUniversalTime() - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds;
}

void PopulateExample(string keyBase, DateTime now, string hostDomainName, string verifyUrl)
{
    const int rounds = 1;

    /* Build request JSON. */
    var requestJson = new JObject();
    //requestJson["Version"] = "BILLPG-DRAFT-4-0";
    requestJson["Host"] = hostDomainName;
    requestJson["Now"] = UnixTime(now);
    requestJson["Unus"] = GenerateUnus(keyBase);
    requestJson["Rounds"] = rounds;
    requestJson["Verify"] = verifyUrl;
    ReplaceJson($"<!--{keyBase}_REQUEST-->", requestJson);

    /* Encode JSON into bytes. */
    requestJson[char.ConvertFromUtf32(0x1F95A)] = "https://billpg.com/nggyu";
    string jsonAsString = requestJson.ToString(Newtonsoft.Json.Formatting.None);
    byte[] jsonAsBytes = Encoding.UTF8.GetBytes(jsonAsString);

    /* Build JSON into an Authorization header. */
    int authHeaderIndex = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_AUTH_HEADER-->"));
    if (authHeaderIndex > 0)
    {
        int authHeaderStartIndex = readmeLines.FindIndex(authHeaderIndex, src => src.StartsWith("Authorization"));
        int authHeaderEndIndx = readmeLines.FindIndex(authHeaderStartIndex+1, src => src.StartsWith(" ") == false);
        readmeLines.RemoveRange(authHeaderStartIndex+1, authHeaderEndIndx - authHeaderStartIndex - 1);

        /* Convert JSON into a BASE64 string. */
        string jsonAsBase64 = Convert.ToBase64String(jsonAsBytes, Base64FormattingOptions.InsertLineBreaks);
        var jsonAsBase64Lines = jsonAsBase64.Split('\r', '\n').Where(s => s.Length > 0).Select(s => " " + s);
        readmeLines.InsertRange(authHeaderStartIndex+1, jsonAsBase64Lines);
    }

    /* Insert the hash of the above JSON into the readme. */
    string hash1066 = HashRequestJsonBytes(jsonAsBytes, rounds);
    int hash1066Index = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_HASH-->"));
    readmeLines[hash1066Index] = $"- `{hash1066}`<!--{keyBase}_HASH-->";

    /* Build response JSON. */
    DateTime issuedAt = now.AddSeconds(1);
    var responseJson = new JObject();
    string jwt = ToBearerToken(new Uri(verifyUrl).Host, hostDomainName, issuedAt, out DateTime expiresAt);
    responseJson["BearerToken"] = GenerateUnus(keyBase + "ShorterBearerToken").Substring(0,40);
    responseJson["IssuedAt"] = UnixTime(issuedAt);
    responseJson["ExpiresAt"] = UnixTime(expiresAt);
    ReplaceJson($"<!--{keyBase}_RESPONSE-->", responseJson);

    /* Build a JWT only response. */
    ReplaceJsonString($"<!--{keyBase}_RESPONSE_JWT_ONLY-->", jwt);

    /* Build a simple token example. */
    responseJson["BearerToken"] = GenerateUnus(keyBase + "SimpleToken").Substring(0, 40);
    ReplaceJson($"<!--{keyBase}_RESPONSE_SIMPLE_TOKEN-->", responseJson);

    /* Build a set-cookie example. */
    int setCookieMarkerIndex = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_SET_COOKIE-->"));
    if (setCookieMarkerIndex > 0)
    {
        int setCookieHeaderIndex = readmeLines.FindIndex(setCookieMarkerIndex, src => src.StartsWith("Set-Cookie"));
        var setCookieValue = GenerateUnus(keyBase + "SetCookie").Substring(0, 40);
        readmeLines[setCookieHeaderIndex] = $"Set-Cookie: MyCookie={setCookieValue}; Secure; HttpOnly;";
    }
}

string HashRequestJsonBytes(byte[] jsonAsBytes, int rounds)
{
    /* Run PBKDF2 over the JSON in byte form. */
    var hash = Rfc2898DeriveBytes.Pbkdf2(
        password: jsonAsBytes,
        salt: fixedSaltBytes,
        hashAlgorithm: HashAlgorithmName.SHA256,
        iterations: rounds,
        outputLength: 256 / 8);

    /* Return as a base-64 string. */
    return Convert.ToBase64String(hash);
}

void ReplaceJson(string tag, JObject insert)
{ 
    /* Look for the "1066" example JSON. */
    int markerIndex = readmeLines.FindIndex(src => src.Contains(tag));
    if (markerIndex < 0) return;
    int openBraceIndex = readmeLines.FindIndex(markerIndex, src => src == "{");
    int closeBraceIndex = readmeLines.FindIndex(openBraceIndex, src => src == "}");
    readmeLines.RemoveRange(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

    /* Insert back into code. */
    readmeLines.Insert(openBraceIndex, insert.ToString().Replace("\r\n  ", "\r\n    "));
}

void ReplaceJsonString(string tag, string value)
{
    /* Look for the tag, then look for the string. */
    int markerIndex = readmeLines.FindIndex(src => src.Contains(tag));
    if (markerIndex < 0) return;
    int stringIndex = readmeLines.FindIndex(markerIndex, src => src.StartsWith("\"ey"));
    readmeLines[stringIndex] = "\"" + value + "\"";
}

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
    expiresAt = issuedAt.ToUniversalTime().AddHours(1).ToUniversalTime();
    long iat = UnixTime(issuedAt);
    long exp = UnixTime(expiresAt);

    string jwtHeader = JWT64Encode(new JObject { ["typ"] = "JWT", ["alg"] = "HS256", [""] = "billpg.com/nggyu" });
    string jwtBody = JWT64Encode(new JObject { ["sub"] = caller, ["iss"] = issuer, ["iat"] = iat, ["exp"] = exp });
    string jwtHeaderDotBody = jwtHeader + "." + jwtBody;

    using var hmac = new System.Security.Cryptography.HMACSHA256();
    hmac.Key = System.Text.Encoding.ASCII.GetBytes("your-256-bit-secret");
    var hash = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes(jwtHeaderDotBody));
    string jwtSig = JWT64EncodeBytes(hash);
    string jwtFull = jwtHeaderDotBody + "." + jwtSig;
    return jwtFull;
}

string JWT64Encode(JObject j)
    => JWT64EncodeString(j.ToString(Newtonsoft.Json.Formatting.None));

string JWT64EncodeBytes(byte[] bytes)
    => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

string JWT64EncodeString(string jsonAsString)
    => JWT64EncodeBytes(System.Text.Encoding.ASCII.GetBytes(jsonAsString));

void SetTextByMarker(List<string> lines, string marker, string line)
{
    int fixedSaltIndex = lines.FindIndex(src => src.Contains(marker));
    lines[fixedSaltIndex] = line + marker;
}

string DumpByteArray(IList<byte> bytes)
{
    string list = "";
    for (int i = 0; i < bytes.Count; i++)
    {
        if (i > 0 && i % 8 == 0)
            list += "\r\n";
        if (i % 8 == 0)
            list += "       ";
        if (i == 0)
            list += "[";
        list += $"{bytes[i]},";
    }

    return list.Trim(',') + "]";
}

string BytesToHex(IList<byte> bytes)
    => string.Concat(bytes.Select(b => b.ToString("X2")));
