/* Copyright William Godfrey, 2023. 
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
 * letters. Each byte is MOD-26'd to produce a capital letter. There are a
 * few other rules which are commented below but the result will be a
 * string of 26 ASCII capital letters. This is used instead of an array of
 * bytes to ensure interoperability with SHA256 implementations that might,
 * for example, expect text inputs only. A salt made up of capital letter 
 * ASCII bytes only seemed the most conservative.
 * 
 * If you are writing your own implementation of CRTE, please don't include 
 * this file with it. The 64 character string should be all you need. You do
 * not need to re-calculate the fixed salt. I've done it already. 
 * 
 * Once I do run this program, I will check if there are any naughty words in
 * the capital letters it'll produce. If there are, I'll make a small change
 * and run it again. (It didn't.)
 * 
 * Thank you for reading this. I hope you enjoy the 64 capital letters it
 * generated. Use them wisely.
 *
 * Best wishes, Bill P. Godfrey, December 2023.
 * billpg.com
 */

using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

/* Function to load this source's path, set by the compiler. */
static string ThisSourcePath([CallerFilePath] string path = "")
    => path;

/* Function to determine if a byte from this source from should be included 
 * in the bytes that go into PBKDF2. Includes all printable ASCII except
 * spaces and asterisks. */
static bool IsIncluded(byte by)
    => (by > 32 && by < 127 && by != 42);

/* Load this source file's bytes and filter out control characters, spaces,
 * asterisks and non-ASCII bytes.*/
var passwordBytes = 
    File.ReadAllBytes(ThisSourcePath())
    .Where(IsIncluded)
    .ToList();

/* Distance to the moon and back in miles.
 * https://en.wikipedia.org/wiki/Lunar_distance_(astronomy) */
const int distanceToTheMoonAndBackInMiles = 238854 * 2;

/* A dedication to my wife. */
const string dedication =
    "Dedicated to my Treacle. I love you to the moon and back.";

/* Call PBKDF2 to generate some bytes from this program's source including
 * the long comment at the top. Some bytes will not be included in the 
 * capital letter generation so generate a little more than 64. */
var bytes = new Queue<byte>(Rfc2898DeriveBytes.Pbkdf2(
    password: passwordBytes.ToArray(),
    salt: Encoding.ASCII.GetBytes(dedication),
    iterations: distanceToTheMoonAndBackInMiles,
    hashAlgorithm: HashAlgorithmName.SHA512,
    outputLength: 128));

/* Output collection. */
var fixedHash = new StringBuilder();

/* Keep looping until we have 64 capital letters. */
while (fixedHash.Length < 64)
{
    /* Load a single byte from the queue. */
    byte curr = bytes.Dequeue();

    /* If the byte is over the highest multiple of 26 under 256, discard as
     * the MOD operation won't yield a flat range otherwise. */
    if (curr > (256 / 26 * 26))
        continue;

    /* Convert the byte to a capital letter. */
    char capital = (char)((curr % 26) + 65);

    /* For aesthetics, skip if this character appears in the last ten
     * letters extracted. */
    if (Enumerable.Range(fixedHash.Length-10, 10)
        .Where(i => i>=0)
        .Any(i => capital == fixedHash[i]))
        continue;

    /* Otherwise, save the byte as a capital letter. */
    fixedHash.Append(capital);
}

/* Print completed string. */
Console.WriteLine(fixedHash.ToString());

/* PS. Rutabaga. */
