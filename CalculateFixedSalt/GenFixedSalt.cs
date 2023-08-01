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
 * Cross-Request-Token-Exchange uses a shared HMAC key as part of the
 * exchange, which will be generated from random strings selected by both
 * the Initiator and Issuer.
 * 
 * The HMAC key itself will be the result of calling PBKDF2 with the 
 * following parameters:
 * 
 * - Password: Intiator's and Issuer's key strings combined.
 * - Salt: Fixed bytes generated in advance.
 * - Hash: SHA256
 * - Rounds: 99
 * 
 * This source code file generates the fixed salt bytes. I will run this
 * code and I will copy the bytes this function generates into the 
 * documentation and eventually into the RFC that specifies this exchange.
 * 
 * That RFC will list the bytes as a list. It will then have a non-normative
 * note that references the version of this file that was used to generate
 * the bytes and invite the reader to repeat the process to confirm the bytes
 * in the RFC are the only bytes this program could have generated.
 *
 * All of the text of this file, including this long comment, is included in
 * the process to generate the salt bytes. The compiler will include this
 * source code's own location and use its contents to generate the salt bytes.
 * Any change to this comment will produce different bytes.
 *
 * Best wishes, Bill P. Godfrey.
 */
internal static class GenFixedSalt
{
    internal static IList<byte> LoadFixedSaltFromCode()
    {
        /* Load this source file's bytes. (Path will be set by the compiler.) */
        var passwordBytes = File.ReadAllText(ThisSourcePath()).Select(c => (byte)c).ToList();
        static string ThisSourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "")
            => path;

        /* Loop through to remove all the control characters and spaces. */
        for (int i = passwordBytes.Count - 1; i >= 0; i--)
        {
            /* If this is a control character, remove it. */
            if (passwordBytes[i] < 33) passwordBytes.RemoveAt(i);

            /* If this is a non-ASCII byte, stop. */
            else if (passwordBytes[i] > 125) 
                throw new Exception("GenFixedSalt.cs contains non-ASCII bytes.");
        }

        /* https://en.wikipedia.org/wiki/Lunar_distance_(astronomy) */
        const int distanceToTheMoonAndBackInMiles = 238854 * 2;

        /* Number of bytes to produce. */
        const int targetByteCount = 198;

        /* Call PBKDF2 to generate the final fixed-salt bytes. */
        return System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password: passwordBytes.ToArray(),
            salt: System.Text.Encoding.ASCII.GetBytes(
                "Dedicated to my Treacle. I love you to the moon and back."),
            iterations: distanceToTheMoonAndBackInMiles,
            hashAlgorithm: System.Security.Cryptography.HashAlgorithmName.SHA512,
            outputLength: targetByteCount).ToList().AsReadOnly();
    }
}

/* PS. Rutabaga. */
