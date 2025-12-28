/* Modify the README.md file with correct tokens and 
 * signatures using the crypto-helper functions. */
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

/* Locate and load README.md into memory. */
string readmePath = FindFileByName("README.md");
var readmeLines = File.ReadAllLines(readmePath).ToList();
var readmeOrigText = string.Join("\r\n", readmeLines);

/* Constants from which the fixed salt bytes will be derived. */
const string fixed_salt_password = "HashBack is dedicated to my Treacle.";
const string fixed_salt_salt = "I love you to the moon and back.";
const int fixed_salt_rounds = 238854 * 2; 

/* Find the fixed salt. */
var fixedSaltBytes = Pbkdf2(
    password: Encoding.ASCII.GetBytes(fixed_salt_password),
    salt: Encoding.ASCII.GetBytes(fixed_salt_salt),
    iterations: fixed_salt_rounds,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 32);

/* Look for the fixed salt in the README. */
SetTextByMarker(readmeLines, "<!--FIXED_SALT_PASSWORD-->", StringSaltParameters("Password", fixed_salt_password));
SetTextByMarker(readmeLines, "<!--FIXED_SALT_DEDICATION-->", StringSaltParameters("Salt", fixed_salt_salt));
SetTextByMarker(readmeLines, "<!--FIXED_SALT_ITERATIONS-->", $"- Iterations: {fixed_salt_rounds}");

/* Look for the fixed salt byte block, two lines after the marker. */
InsertByteArrayAsText(readmeLines, "<!--FIXED_SALT-->", "```", "```", fixedSaltBytes);

/* Look for the line with the fixed salt in hex/base64. */
SetTextByMarker(readmeLines, "<!--FIXED_SALT_HEX-->", $"- Hex: `{BytesToHex(fixedSaltBytes)}`");
SetTextByMarker(readmeLines, "<!--FIXED_SALT_B64-->", $"- Base64: `{Convert.ToBase64String(fixedSaltBytes)}`");
SetTextByMarker(readmeLines, "<!--FIXED_SALT_URL-->", $"- URL: `{System.Web.HttpUtility.UrlEncode(fixedSaltBytes)}`");

/* Rewrite the HashBackCore copy of the fixed salt in source. */
string helpersPath = FindFileByName("Helpers.cs");
var helpersLines = File.ReadAllLines(helpersPath).ToList();
InsertByteArrayAsText(helpersLines, "FixedSalt42", "[", "]", fixedSaltBytes);
File.WriteAllLines(helpersPath, helpersLines);

/* Pull out the copy of the fixed salt in memory and complain if it is different. */
var helperFixedSalt = 
    (byte[])
    typeof(billpg.HashBackCore.Helpers)
    .GetField("FixedSalt42", BindingFlags.NonPublic | BindingFlags.Static)!
    .GetValue(null)!;
if (Convert.ToBase64String(helperFixedSalt) != Convert.ToBase64String(fixedSaltBytes))
{
    Console.WriteLine("Rebuild and run this app again.");
    return;
}


/* Populate the main examples in the README. */
PopulateExample(
    "1066_EXAMPLE",
    DateTime.Parse("1986-10-09T23:00:00-04:00"),
    "server.example",
    "client.example");

/* Populate the case study. */
PopulateExample(
    "CASE_STUDY",
    DateTime.Parse("1991-08-20T23:02:00+03:00"),
    "RutabagaRepublic.example",
    "Petunia.example");

/* If README has changed, rewrite back. */
if (readmeOrigText != string.Join("\r\n", readmeLines))
{
    Console.WriteLine("Saving modified README.md.");
    File.WriteAllLines(readmePath, readmeLines, new UTF8Encoding(true));
}

/* Announce end. */
Console.WriteLine("Finished helper/readme update.");

/* Run PBKDF2 to generate the fixed salt, refering to a cache if we have it. */
byte[] Pbkdf2(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm, int outputLength)
{
    /* Check if we've done this exact hashing operation before. */
    string cacheFixedSaltPath =
        Path.Combine(Path.GetTempPath(),
        "CacheFixedSaltPath" +
        $"_{BytesToHex(password)}" +
        $"_{BytesToHex(salt)}" +
        $"_{iterations}" +
        $"_{hashAlgorithm}" +
        $"_{outputLength}.bin");
    if (File.Exists(cacheFixedSaltPath))
        return File.ReadAllBytes(cacheFixedSaltPath);

    /* If not call through to the actual PBKDF2. */
    var hashTimer = Stopwatch.StartNew();
    var fixedSaltAsBytes = Rfc2898DeriveBytes.Pbkdf2(
        password: password,
        salt: salt,
        iterations: iterations,
        hashAlgorithm: hashAlgorithm,
        outputLength: outputLength);
    Console.WriteLine($"PBKDF2 took {hashTimer.Elapsed}");

    /* Save to cache. */
    File.WriteAllBytes(cacheFixedSaltPath, fixedSaltAsBytes);

    /* Return now cached bytes to caller. */
    return fixedSaltAsBytes;
}

/* Expand the salt parameter strings into a longer entry, clarifying their length and byte sum. */
string StringSaltParameters(string label, string value)
{
    int byteCount = value.Length;
    int byteSum = value.Select(ch => (int)ch).Sum();
    return $"- {label}: \"{value}\" ({byteCount} bytes, summing to {byteSum}.)";
}

/* Look for markers in the readme to populate with specific examples. */
void PopulateExample(string keyBase, DateTime now, string hostDomainName, string clientDomainName)
{
    /* Generate a string to use as the verify URL. */
    var verifyUrl = new UriBuilder("https", clientDomainName)
    {
        Path = "/api/hashback", 
        Query = $"?id={GenerateDecimal(keyBase)}"
    }.ToString();
    string xnHostDomain = new System.Globalization.IdnMapping().GetAscii(hostDomainName);

    /* Use HashBackCore to build the Authorization header JSON. */
    var builder = new billpg.HashBackCore.HashBackBuilder();
    builder.Host = hostDomainName;
    builder.NowGetter = () => billpg.HashBackCore.Helpers.ToUnixTimeSeconds(now);
    builder.UnusGetter = () => GenerateUnus(128, keyBase);
    builder.SetVerify(verifyUrl);
    string verificationHash = "";
    builder.SetSyncHashRegister((url, hash) => verificationHash = hash);
    var authHeader = builder.Build().Result;

    /* Bring the base64 block back into bytes and parse as JSON. */
    byte[] jsonAsBytes = Convert.FromBase64String(authHeader);
    string jsonAsString = Encoding.UTF8.GetString(jsonAsBytes);
    var requestJson = JObject.Parse(jsonAsString);

    /* Insert JSON into readme. */
    ReplaceJson($"<!--{keyBase}_REQUEST-->", requestJson);

    /* Build JSON into an Authorization header. */
    int authHeaderIndex = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_AUTH_HEADER-->"));
    if (authHeaderIndex > 0)
    {
        /* The HTTP Host: header requies xn-- notation for domains. */
        int hostHeaderIndex = readmeLines.FindIndex(authHeaderIndex, src => src.StartsWith("Host:"));
        if (hostHeaderIndex > 0 && hostHeaderIndex < readmeLines.Count)
        {
            readmeLines[hostHeaderIndex] = $"Host: {xnHostDomain}";
        }

        int authHeaderStartIndex = readmeLines.FindIndex(authHeaderIndex, src => src.StartsWith("Authorization"));
        int authHeaderEndIndx = readmeLines.FindIndex(authHeaderStartIndex+1, src => src.StartsWith(" ") == false);
        readmeLines.RemoveRange(authHeaderStartIndex+1, authHeaderEndIndx - authHeaderStartIndex - 1);

        /* Convert JSON into a BASE64 string. */
        string jsonAsBase64 = Convert.ToBase64String(jsonAsBytes, Base64FormattingOptions.InsertLineBreaks);
        var jsonAsBase64Lines = jsonAsBase64.Split('\r', '\n').Where(s => s.Length > 0).Select(s => " " + s);
        readmeLines.InsertRange(authHeaderStartIndex+1, jsonAsBase64Lines);
    }

    /* Insert the hash of the above JSON into the readme. */    
    int hash1066Index = readmeLines.FindIndex(src => src.Contains($"<!--{keyBase}_HASH-->"));
    if (hash1066Index > 0)
    {
        var lineByQuotes = readmeLines[hash1066Index].Split('`');
        readmeLines[hash1066Index] = lineByQuotes[0] + "`" + verificationHash + "`" + lineByQuotes[2];
    }

    /* Build Set-Cookie header. */
    int responseMarkerLineIndex = readmeLines.FindIndex(s => s.Contains($"<!--{keyBase}_SET_COOKIE-->"));
    if (responseMarkerLineIndex > 0)
    {
        string setCookieHeader =
            $"Set-Cookie:" +
            $" RutabagaAuth={GenerateBearerToken(keyBase)};\r\n" +
            $"  Domain={xnHostDomain};\r\n" +
            $"  Expires={now.AddSeconds(4601):R};\r\n" +
            $"  Secure; HttpOnly; SameSite=Strict";
        int setCookieLineIndex = readmeLines.FindIndex(responseMarkerLineIndex, s => s.StartsWith("Set-Cookie:"));
        int endSetCookie = readmeLines.FindIndex(setCookieLineIndex+1, s => s.StartsWith(' ') == false);
        readmeLines[setCookieLineIndex] = setCookieHeader;
        readmeLines.RemoveRange(setCookieLineIndex + 1, endSetCookie - setCookieLineIndex - 1);
    }
}

/* Generate a deterministic decimal number for examples. */
int GenerateDecimal(string key)
{
    var valueAsBase64 = GenerateUnus(32, key + "GenerateDecimal");
    var valueAsBytes = Convert.FromBase64String(valueAsBase64);
    valueAsBytes[3] &= 0x7F;
    var valueAsInt = BitConverter.ToInt32(valueAsBytes);
    return (valueAsInt % 1000000000);
}

/* Generate a random-looking token that would work as a non-JWT bearer token. */
string GenerateBearerToken(string keyBase)
{
    /* Loop through six times, adding hyphens. */
    string token = "";
    for (int i = 0; i < 3; i++)
    {
        /* Dot separator, except first time. */
        if (i > 0)
            token += ".";

        /* Add some random looking characters. */
        string random = GenerateUnus(256, keyBase + "SimpleBearer" + i).Replace("+","").Replace("/", "");
        token += random.Substring(0, 8);
    }

    /* Completed token. */
    return token;
}

void ReplaceJson(string tag, JObject insert)
{
    /* Turn the JSON object into a Json string with only ascii. */
    StringBuilder jsonAsString = new StringBuilder(insert.ToString().Replace("\r\n  ", "\r\n    "));
    for (int jsonIndex = jsonAsString.Length-1; jsonIndex > 0; jsonIndex--)
    {
        char ch = jsonAsString[jsonIndex];
        if (ch > 126)
        {
            jsonAsString.Remove(jsonIndex, 1);
            jsonAsString.Insert(jsonIndex, $"\\u{(int)ch:X4}");
        }
    }

    /* Look for the "1066" example JSON. */
    int markerIndex = readmeLines.FindIndex(src => src.Contains(tag));
    if (markerIndex < 0) return;
    int openBraceIndex = readmeLines.FindIndex(markerIndex, src => src == "{");
    int closeBraceIndex = readmeLines.FindIndex(openBraceIndex, src => src == "}");
    readmeLines.RemoveRange(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

    /* Insert back into code. */
    readmeLines.Insert(openBraceIndex, jsonAsString.ToString());
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
string GenerateUnus(int bits, string v)
{
    /* Hash the input string and return in hex. */
    var hash = Rfc2898DeriveBytes.Pbkdf2(
        password: Encoding.ASCII.GetBytes(v),
        salt: Encoding.ASCII.GetBytes("no salt this time"),
        hashAlgorithm: HashAlgorithmName.SHA256,
        iterations: 1,
        outputLength: bits / 8);
    return Convert.ToBase64String(hash);
}

void SetTextByMarker(List<string> lines, string marker, string line)
{
    int fixedSaltIndex = lines.FindIndex(src => src.Contains(marker));
    lines[fixedSaltIndex] = line + marker;
}

static void InsertByteArrayAsText(List<string> lines, string beacon, string startMarker, string endMarker, byte[] bytes)
{
    /* Find the markers, starting from the unique beacon. */
    int beaconIndex = lines.FindIndex(src => src.Contains(beacon));
    int startMarkerIndex = lines.FindIndex(beaconIndex + 1, src => src.Contains(startMarker));
    int endMarkerIndex = lines.FindIndex(startMarkerIndex + 1, src => src.Contains(endMarker));

    /* Pull out the indent to use from the first line in the block already there. */
    int indentCount = lines[startMarkerIndex+1].TakeWhile(ch => ch == ' ').Count();

    /* Clear out existing lines between the markers. */
    lines.RemoveRange(startMarkerIndex + 1, endMarkerIndex - startMarkerIndex - 1);

    /* Insert the byte array, 8 bytes per line. */
    string? currLine = null;
    int insertIndex = startMarkerIndex + 1;
    for (int i = 0; i < bytes.Length; i++)
    {
        /* New line every 8 bytes. */
        if (i % 8 == 0)
        {
            if (currLine != null)
            {
                lines.Insert(insertIndex, currLine);
                insertIndex++;
            }
            currLine = new string(' ', indentCount);
        }
        currLine += $"{bytes[i]},";
    }

    /* Remove trailing comma and insert last line. */
    lines.Insert(insertIndex, currLine!.TrimEnd(','));
}

string BytesToHex(IList<byte> bytes)
    => string.Concat(bytes.Select(b => b.ToString("X2")));
