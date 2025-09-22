using billpg.HashBackCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static billpg.HashBackCore.HashBackValidator;

namespace HashBackCoreTests
{
    internal static class ValidatorTestHelpers
    {
        internal static bool EqualsOneBillion(long nowSupplied)
            => nowSupplied == (long)1E9;

        internal static Task<string?> ExtractUrlHost(string url)
            => Task.FromResult<string?>(new Uri(url).Host);

        internal static OnGetHashDelegate HashGetterFromHeader(byte[] authBytes)
        {
            /* Pre-caclulate the expected hash for these bytes. */
            string hash = Helpers.ComputeVerificationHash(authBytes);

            /* Return hash wrapped in a task object wrapped in a delegate. */
            return url => Task.FromResult(hash);
        }

        internal static OnGetHashDelegate HashGetterFromHeader(string authBase64)
            => HashGetterFromHeader(Convert.FromBase64String(authBase64));

        internal static OnGetHashDelegate HashGetterFromClearJson(string authJson)
            => HashGetterFromHeader(Encoding.ASCII.GetBytes(authJson));


    }
}
