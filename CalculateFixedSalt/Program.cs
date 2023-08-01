#nullable disable

/* Generate the 999 bytes that will form the fixed PBKDF2 salt. */
IList<byte> fixedSalt = GenFixedSalt.LoadFixedSaltFromCode();

/* Look for the CryptoHelpers.cs file and find the lines where the bytes sit. */
const string helperSourcePath = "../../../../billpg.CrossRequestTokenExchange/CryptoHelpers.cs";
var helpers = File.ReadAllLines(helperSourcePath).ToList();
int bytesStartLineIndex = FirstLineContains(helpers, 0, "FixedPbkdf2Salt");
int bytesEndLineIndex = FirstLineContains(helpers, bytesStartLineIndex+1, "AsReadOnly");
helpers.RemoveRange(bytesStartLineIndex + 1, bytesEndLineIndex - bytesStartLineIndex - 1);

/* Loop through the 999 bytes 27 bytes at a time. */
const int blockSize = 18;
foreach (int byteBlock in Enumerable.Range(0, fixedSalt.Count/blockSize))
{
    var lineOfBytes = string.Join(",", fixedSalt.Skip(byteBlock * blockSize).Take(blockSize));
    helpers.Insert(bytesStartLineIndex + 1 + byteBlock, new string(' ', 12) + lineOfBytes + ",");
}

/* Find the last line and remove the trailing comma. */
int bytesNewLastLineIndex = FirstLineContains(helpers, bytesStartLineIndex, "AsReadOnly")-1;
helpers[bytesNewLastLineIndex] = helpers[bytesNewLastLineIndex].TrimEnd(',');

/* Save the helper source back. */
File.WriteAllLines(helperSourcePath, helpers);

int FirstLineContains(IList<string> lines, int startIndex, string key)
{
    foreach (int lineIndex in Enumerable.Range(startIndex, lines.Count))
    {
        if (lines[lineIndex].Contains(key))
            return lineIndex;
    }
    return -1;
}
