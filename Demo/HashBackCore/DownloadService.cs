using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class DownloadService
    {
        public string RootUrl { get; set; } = null!;
        public OnErrorFn OnDownloadError { get; set; }
            = msg => throw new NotImplementedException();
        public IList<string> AlwaysAllow { get; set; }
            = new List<string>().AsReadOnly();

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
            var hls = new HostLookupService();
            var xreq =
                Spartan.Request
                .GET(verifyUrl)
                .WithHeader("Accept", "text/plain");
            var resp = Spartan.Run(xreq, msg => new ApplicationException(msg), hls.Lookup);

            return resp.Body.Trim();
#if false
            HttpClient http = new HttpClient();
            var result = http.GetAsync(verifyUrl.ToString()).Result;
            var resultBody = result.Content.ReadAsStringAsync().Result;
            return resultBody.Trim();
#endif
        }
    }
}
