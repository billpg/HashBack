using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

/* Function to load this source's path, set by the compiler. */
static string ThisSourcePath([CallerFilePath] string path = "")
    => path;

/* Loop through each byte of this source file and if it isn't a control, 
 * space, non-ASCII or asterisk, save it into a byte collection. Spaces and
 * controls are removed to avoid line ending issues and to allow the layout
 * to change. Asterisks are also removed to allow multi-line comments to be
 * reformatted.) */
List<byte> passwordBytes = new();
foreach (byte by in File.ReadAllBytes(ThisSourcePath()))
    if (by > 32 && by < 127 && by != 42)
        passwordBytes.Add(by);

/* Distance to the moon and back in miles.
 * https://en.wikipedia.org/wiki/Lunar_distance_(astronomy) */
const int distanceToTheMoonAndBackInMiles = 238854 * 2;

/* A dedication. */
const string dedication =
    "Dedicated to my Treacle. I love you to the moon and back.";

/* Call PBKDF2 for some deterministic yet random looking bytes. 
 * The first half is used to shuffle the alphabet while the second half is to
 * select letters from the shuffled alphabet to use in the fixed salt. */
var bytes = new Queue<byte>(Rfc2898DeriveBytes.Pbkdf2(
    password: passwordBytes.ToArray(),
    salt: Encoding.ASCII.GetBytes(dedication),
    iterations: distanceToTheMoonAndBackInMiles,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 128));

/* The capital letters will be selected from this collection, starting off as
 * the letters from the famous sentence that includes all letters. Each loop
 * will result in a letter from the first 16 selected and moved to the end of
 * the alphabet. This ensures that each letter never appears too close to 
 * another copy of itself. */
List<char> alphabet = new("THEQUICKBROWNFXJMPDVLAZYGS");

/* Output collection. */
var fixedHash = new StringBuilder();

/* Keep looping until the bytes queue is exhausted. */
while (bytes.Any())
{
    /* Load a single index from the queue, 
     * discarding the top four bits of each byte. */
    int index = bytes.Dequeue() % 16;

    /* Load the corresponding capital and move it to the end of the alphabet. */
    char capital = alphabet[index];
    alphabet.RemoveAt(index);
    alphabet.Add(capital);

    /* If we're in the last 64 loops, store the extracted capital letter. */
    if (bytes.Count < 64)
        fixedHash.Append(capital);
}

/* Output the extracted capitals. */
Console.WriteLine(fixedHash.ToString());

/* PS. Rutabaga. */
