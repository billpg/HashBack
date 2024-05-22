using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class DownloadService
    {
        public Uri RootUrl { get; set; } = null!;
        public OnErrorFn OnDownloadError { get; set; }
            = msg => throw new NotImplementedException();
        public IList<string> AlwaysAllow { get; set; }
            = new List<string>().AsReadOnly();
        public OnHostLookupFn OnHostLookup { get; set; } 
            = host => throw new NotImplementedException();

        public void ConfigureHttpService(WebApplication app, string path)
        {
            app.MapGet(path, this.AllowDownloadHandler);
        }

        private IResult AllowDownloadHandler(HttpContext context)
        {
            return Results.Text("Hello.");
        }

        public OnRetrieveVerifyHashFn HashDownload => HashDownloadInternal;
        private string HashDownloadInternal(Uri verifyUrl)
        {
            /* Is the supplied URL valid? Will throw exception if not. */
            ValidateVerifyUrl(verifyUrl);

            /* Passed validation. Actually make the call. */
            var xreq =
                Spartan.Request
                .GET(verifyUrl)
                .WithHeader("Accept", "text/plain");
            var resp = Spartan.Run(xreq, this.OnDownloadError, this.OnHostLookup);

            /* If not 200, respond with error message. */
            if (resp.StatusCode != 200)
                throw OnDownloadError(
                    $"Server at {verifyUrl.Host} returned status code " +
                    $"{resp.StatusCode}. Expected 200.");

            /* Return the downloaded hash. */
            return resp.Body.Trim();
        }

        private void ValidateVerifyUrl(Uri url)
        {
            /* Allow this service's own hash service. */
            if (url.Root() == this.RootUrl.Root())
                return;

            /* Disallow any scheme but HTTPS. */
            if (url.Scheme != "https")
                throw OnDownloadError("Requested URL is not HTTPS.");

            /* Disallow localhost.
             * (Unless it was allowed by being part of the RootUrl earlier.) */
            if (url.Host.ToLowerInvariant() == "localhost")
                throw OnDownloadError("Requested URL cannot be localhost.");

            /* Disallow any port other than 443.
             * (Also unless it was part of the RootUrl.) */
            if (url.Port != 443)
                throw OnDownloadError("Requested URL must be 443.");

            /* Check the domain is part of the allowed list.
             * TODO: Write a way for the public to add themselves. */
            if (this.AlwaysAllow.Contains(url.Host) == false)
                throw OnDownloadError(
                    $"The host {url.Host} is not on the list of allowed domains.\r\n"
                    + "\r\n"
                    + "Please open a ticket at https://github.com/billpg/HashBack/issues\r\n"
                    + "if you'd like me to add yours before I write a way for the public\r\n"
                    + "to add themsleves to the list.");
        }
    }
}
