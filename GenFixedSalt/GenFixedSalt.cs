/* Copyright William Godfrey, 2024. 
 * 
 * This file, "GenFixedSalt.cs", may be freely copied under the terms of the
 * Creative Commons Attribution-NoDerivs license.
 * https://creativecommons.org/licenses/by-nd/4.0/
 * 
 *                    CROSS REQUEST TOKEN EXCHANGE
 *                         CRTE-PUBLIC-DRAFT-3
 *
 * This exchange confirms the authenticity of a participant by returning the
 * result of running SHA256 on the JSON request body supplied by that
 * participant.
 *
 * So that implementations will only interoperate by the documented standard,
 * the full JSON request body, including a one-time-key, is passed into 
 * SHA256 along with a "salt" which is fixed in advance and is not included 
 * in the traffic sent over the wire. This program will generates some 
 * pseudo-random bytes and convert them into 64 capital letters which will
 * be used as the salt bytes that go into SHA256.
 * 
 * All of the text of this file, including this long comment, is included in
 * the process to generate the salt bytes. The compiler will insert this 
 * file's own location in order to load these bytes. Each byte takes part in 
 * the process to generate the salt. Any change to this comment will cause 
 * different bytes to be produced.
 * 
 * I invite you to run this program and check that the string of capital
 * letters match the ones in the specification document. 
 * ("Nothing up my sleeve.")
 * 
 * Not all the bytes of this source, however. The spaces and control 
 * characters are removed to avoid different results with formatting the code
 * and CRLF/CR/LF issues. The asterisks are also taken out because this
 * multi-line comment has them and reflowing the comments would cause 
 * different results too.
 * 
 * PBKDF2 is used to produce sufficient bytes to produce those capital
 * letters. Each time the code loops, one of 16 letters is selected as
 * that letter is moved to the end of the selection of 26. This ensures,
 * for aesthetic reasons, that no capital is repeated within ten positions
 * of itself. 
 * 
 * This is used instead of an array of bytes to ensure interoperability with
 * SHA256 implementations that might, for example, expect text inputs only.
 * A salt made up of capital letter ASCII bytes only seemed the most
 * conservative.
 * 
 * If you are writing your own implementation of CRTE, please don't include 
 * this file with it. The 64 character string should be all you need. You do
 * not need to re-calculate the fixed salt. I've done it already. 
 * 
 * Once I do run this program, I will check if there are any naughty words in
 * the capital letters it'll produce. If there are, I'll make a small change
 * and run it again. (It didn't.)
 * 
 * I intend for this exchange to become a public RFC, with this code as an
 * appendix. In this event, I'd need to re-write this comment, especially the
 * copyright notice, include the RFC number and remove the "draft" version 
 * tag. As such, the 64 characters produced by this is good enough for the third
 * public draft, but future versions will produce a new salt.
 * 
 * Thank you for reading this. I hope you enjoy the 64 capital letters it
 * generated. Use them wisely.
 *
 * Best wishes, Bill P. Godfrey, January 2024.
 * billpg.com
 */

using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

/* Function to load this source's path, set by the compiler. */
static string ThisSourcePath([CallerFilePath] string path = "")
    => path;

/* Loop through each byte of this source file and if it isn't a control, 
 * non-ASCII or asterisk, save it into a byte collection. */
List<byte> passwordBytes = new();
foreach (byte by in File.ReadAllBytes(ThisSourcePath()))
    if (by > 32 && by < 127 && by != 42)
        passwordBytes.Add(by);

/* Distance to the moon and back in miles.
 * https://en.wikipedia.org/wiki/Lunar_distance_(astronomy) */
const int distanceToTheMoonAndBackInMiles = 238854 * 2;

/* A dedication to my wife. */
const string dedication =
    "Dedicated to my Treacle. I love you to the moon and back.";

/* Call PBKDF2 to generate a kilobyte of pseudo-random bytes. */
var bytes = new Queue<byte>(Rfc2898DeriveBytes.Pbkdf2(
    password: passwordBytes.ToArray(),
    salt: Encoding.ASCII.GetBytes(dedication),
    iterations: distanceToTheMoonAndBackInMiles,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 999));

/* The capital letters will be selected from this collection, starting off as
 * the letters from the famous sentence that includes all letters. */
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

/* Print the extracted capitals. */
Console.WriteLine(fixedHash.ToString());

/* PS. Rutabaga. */
