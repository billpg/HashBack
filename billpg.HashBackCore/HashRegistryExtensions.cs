using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class HashRegistryExtensions
    {
        public static AuthorizationBuilder WithHashRegistry(
            this AuthorizationBuilder builder,
            string baseUrl,
            string queryParamName,
            Func<Guid> guidGenerator,
            Action<Guid, string> register)
        {
            /* Return a new builder wrapped with a custom verify 
             * URL generator and a custom post-build handler that
             * pulls the guid out and passes it to the register function. */
            return builder
                .WithVerifyGenerator(InternalVerifyGenerator)
                .WithPostBuild(InternalPostBuild);

            /* Generated URL from parameters and a new GUID. */
            string InternalVerifyGenerator()
                => new UriBuilder(baseUrl)
                { Query = $"?{queryParamName}={guidGenerator()}" }
            .Uri.AbsoluteUri;

            /* Extract the GUID from the Verify URL and register it with the hash. */
            void InternalPostBuild(AuthorizationBuildResult result)
            {
                /* Extract the GUID from the Verify URL. */
                var uri = new Uri(result.VerifyUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                string idString = query.Get(queryParamName) 
                    ?? throw new InvalidOperationException("Could not find GUID in Verify URL.");
                if (!Guid.TryParse(idString, out Guid id))
                    throw new InvalidOperationException("Could not parse GUID from Verify URL.");

                /* Register the GUID and expected hash using the provided action. */
                register(id, result.VerificationHash);
            }
        }            
    }
}
