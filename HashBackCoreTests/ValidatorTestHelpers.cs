using billpg.HashBackCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCoreTests
{
    internal static class ValidatorTestHelpers
    {
        internal static AuthHeaderValidator WithMockUserIdentifierFromUrlHost(this AuthHeaderValidator valid)
        {
            /* Wrap this validator with a mock user identifier. */
            return valid.WithUserIdentifier(Mock);

            /* Mock user identifier, always returns the
             * host portion of the verification URL. */
            static Task<string> Mock(string url)
                => Task.FromResult(new Uri(url).Host);            
        }

        internal static AuthHeaderValidator WithMockCorrectHashGetter(
            this AuthHeaderValidator valid, 
            byte[] authBytes)
        {
            /* Pre-caclulate the expected hash for these bytes. */
            string hash = Helpers.ComputeVerificationHash(authBytes);

            /* Wrap this validator with a mock hash getter. */
            return valid.WithVerificationHashGetter(Mock);

            /* Hash getter that returns the hash calaulate earlier, ignoring the URL. */
            Task<string> Mock(string url)
                => Task.FromResult(hash);
        }
    }
}
