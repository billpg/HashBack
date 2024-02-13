using System.Text;
using System.Security.Cryptography;

/* Call PBKDF2 for some deterministic yet random looking bytes. 
 * The first half is used to shuffle the alphabet while the second half is to
 * select letters from the shuffled alphabet to use in the fixed salt. */
var bytes = new Queue<byte>(Rfc2898DeriveBytes.Pbkdf2(
    password: Encoding.ASCII.GetBytes("HashBack"),
    salt: Encoding.ASCII.GetBytes(
        "Dedicated to my Treacle. I love you to the moon and back."),
    iterations: 238854 * 2,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 128));

/* The capital letters will be selected from this collection, starting off as
 * the letters from the famous sentence that includes all letters. Each loop
 * will result in a letter from the first 16 selected and moved to the end of
 * the alphabet. This ensures that each letter never appears too close to 
 * another copy of itself. */
List<char> alphabet = new("THEQUICKBROWNFXJMPDVLAZYGS");

/* Keep looping until the bytes queue is exhausted. */
var fixedHash = new StringBuilder();
while (bytes.Any())
{
    /* Load a single index (0-15) from the queue, 
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
