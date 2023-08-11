/* This file is Copyright William Godfrey, 2023.
 * 
 * It may be freely copied under the terms of the 
 * Creative Commons Attribution-NoDerivs license.
 * https://creativecommons.org/licenses/by-nd/3.0/
 * 
 *                        _____ ______ _____ _____ 
 *                       /  __ \| ___ \_   _|  ___|
 *                       | /  \/| |_/ / | | | |__  
 *                       | |    |    /  | | |  __| 
 *                       | \__/\| |\ \  | | | |___ 
 *                        \____/\_| \_| \_/ \____/                           
 * 
 * Cross-Request-Token-Exchange uses a shared HMAC key as part of the exchange,
 * which will be generated using PBKDF2 each time it runs, with a low 99 
 * iteration count.
 *
 * So that implementations will only interoperate by the documented standard,
 * that PBKDF2's "Salt" parameter is fixed in advance and is not included in the
 * traffic sent over the wire. This program generates some random bytes and
 * converts them into 120 capital letters.
 * 
 * All of the text of this file, including this long comment, is included in
 * the process to generate the salt bytes. The compiler will insert this
 * file's own location in order to load these bytes. Each byte (except the 
 * spaces and control codes) takes part in the process to generate the salt.
 * Any change to this comment will cause different bytes to be produced.
 * 
 * 120 capital letters requires 71 bytes (568 bits). The bytes from this file
 * go into a once-off PBKDF2 to produce those 71 bytes, but this time with a
 * high iteration count.
 * 
 * If you writing your own implementation of CRTE, please don't include this
 * file with it. (To be clear, you can. The CC text linked at the top of this
 * file lets you. Nonetheless, please don't.) The 120 character string should
 * be all you need. You do not need to re-calculate the fixed salt. I've done
 * it already. (Or I will have by the time you read this. I am writing this
 * from the past. Woo time-travel woo!)
 * 
 * Once I do run this program, I will check if there any naughty words in the
 * capital letters it'll produce. If there are, I'll make a small change and
 * run it again. If you see this message, it means I didn't.
 * 
 * Thank you for reading this. I hope you enjoy the 120 capital letters it 
 * generated.
 *
 * Best wishes, Bill P. Godfrey, 2023.
 */

using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

internal static class GenFixedSalt
{
    internal static string LoadFixedSaltFromCode()
    {
        /* Load this source file's bytes. (Path will be set by the compiler.) */
        var passwordBytes = File.ReadAllBytes(ThisSourcePath()).ToList();
        static string ThisSourcePath([CallerFilePath] string path = "")
            => path;

        /* Loop through to remove all the control characters and spaces. */
        foreach (int i in Enumerable.Range(0, passwordBytes.Count).Reverse())
        {
            /* If this is a control character, space or not-ASCII, remove it. */
            if (passwordBytes[i] < 33 || passwordBytes[i] > 126)
                passwordBytes.RemoveAt(i);
        }

        /* Distance to the moon and back in miles.
         * https://en.wikipedia.org/wiki/Lunar_distance_(astronomy) */
        int distanceToTheMoonAndBackInMiles = 238854 * 2;

        /* How many available capitals? */
        int targetBase = 'Z' - 'A' + 1;

        /* How many bits does a single capital represent? */
        double targetBaseBitValue = Math.Log2(targetBase);

        /* How many capitals do we want? */
        int targetCapitalCount = 120;

        /* How many bytes do we need for that many capitals? */
        int intermediateByteBlockSize = 
            (int)Math.Ceiling(targetCapitalCount * targetBaseBitValue / 8);

        /* Call PBKDF2 to generate some bytes that will be converted into capitals. */
        Queue<byte> bytes = new Queue<byte>(Rfc2898DeriveBytes.Pbkdf2(
            password: passwordBytes.ToArray(),
            salt: Encoding.ASCII.GetBytes(
                "Dedicated to my Treacle. I love you to the moon and back."),
            iterations: distanceToTheMoonAndBackInMiles,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: intermediateByteBlockSize));

        /* The mutable current value. During the loop, when needed, it'll shift the 
         * value along by eight bits and insert a new byte into the gap. */
        int currValue = 0;

        /* How many bits does currValue represent? Stored as a double because 26
         * is not a power of two. */
        double currBitCount = 0;

        /* Keep looping until we have the sought number of capital letters. */
        var capitals = new List<int>();
        while (capitals.Count < targetCapitalCount)
        {
            /* Do we need to add another byte? */
            if (currBitCount < targetBaseBitValue)
            {
                /* Shift what we have along and insert a new byte into the gap. */
                currValue <<= 8;
                currValue |= bytes.Dequeue();

                /* We've 8 bits worth to the current value. */
                currBitCount += 8;
            }

            /* Pull out a single capital. */
            capitals.Add(currValue % targetBase);

            /* Lower the value and the number of bits it represents. */
            currValue /= targetBase;
            currBitCount -= targetBaseBitValue;
        }

        /* Check the byte queue was completely emptied. (If the error was the 
         * other way, .Dequeue would have thrown an exception earlier.) */
        if (bytes.Count != 0)
            throw new Exception("Byte Queue was not left empty by the end of the loop.");

        /* Turn the base64 digits into actual capitals and return. */
        return new string(capitals.Select(cap => (char)(65+cap)).ToArray());
    }
}

/* PS. Rutabaga. */
