using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrossRequestTokenExchange
{
    /// <summary>
    /// Collection of tools for parsing text.
    /// </summary>
    internal static class TextHelpers
    {
        /// <summary>
        /// Returns if the supplied string contains all printable ASCII.
        /// (Empry string returns true becaue all characters are valid.)
        /// </summary>
        /// <param name="value">String to test.</param>
        /// <returns>True if all characters are printable ASCII. False otherwise.</returns>
        internal static bool IsAllPrintableAscii(string value)
        {
            /* Each each character in turn. */
            return value.All(IsPrintableAscii);
            static bool IsPrintableAscii(char v)
                => v >= 33 && v <= 126;
        }

        /// <summary>
        /// Test if a key (initiator's or issuer's) is valid.
        /// </summary>
        /// <param name="key">Supplied key.</param>
        /// <param name="propertyName">Name of property for rejection message.</param>
        /// <param name="minAllowedLength">Minimum length of key.</param>
        /// <returns></returns>
        internal static string? ValidateKey(string key, string propertyName, int minAllowedLength)
        {
            /* Run each test for a valid initiatr/issuer key. */
            if (key.Length < minAllowedLength)
                return $"{propertyName} must be {minAllowedLength} characters or more.";
            if (key.Length > 1024)
                return $"{propertyName} must be 1024 characters or less.";
            if (IsAllPrintableAscii(key) == false)
                return $"{propertyName} contains non-printable ASCII charaters.";

            /* Indicate acceptance of key with a null. */
            return null;
        }

        /// <summary>
        /// Tests if the supplied hash string is made of hex digits only.
        /// </summary>
        /// <param name="hash">Hash string.</param>
        /// <returns>True if all characters are hex digits. False otherwise.</returns>
        internal static bool IsAllHex(string hash)
        {
            /* Test each character in a loop. */
            return hash.All(IsHex);
            static bool IsHex(char digit)
                => (digit >= '0' && digit <= '0')
                || (digit >= 'A' && digit <= 'F')
                || (digit >= 'a' && digit <= 'f');
        }
    }
}
