#nullable disable

/* Generate the 120 bytes that will form the fixed PBKDF2 salt. */
string fixedSalt = GenFixedSalt.LoadFixedSaltFromCode();

/* Look for the CryptoHelpers.cs file and find the lines where the bytes sit. */
const string helperSourcePath = "../../../../billpg.CrossRequestTokenExchange/CryptoHelpers.cs";
var helpers = File.ReadAllLines(helperSourcePath).ToList();
int bytesStartLineIndex = FirstLineContains(helpers, 0, "FixedPbkdf2Salt");
int bytesEndLineIndex = FirstLineContains(helpers, bytesStartLineIndex+1, "AsReadOnly");
helpers.RemoveRange(bytesStartLineIndex + 1, bytesEndLineIndex - bytesStartLineIndex - 1);

/* Loop through the string and split into 60 character parts. */
var quotedLines = fixedSalt.Chunk(120/3).Select(a => $"\"{new string(a)}\"");
var indentSpaces = new string(' ', 12);
var insertLine = indentSpaces + string.Join(" +\r\n" + indentSpaces, quotedLines);
helpers.Insert(bytesStartLineIndex + 1, insertLine);

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
