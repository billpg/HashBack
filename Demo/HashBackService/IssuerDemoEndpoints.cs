using billpg.HashBackCore;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackService
{
    internal class IssuerDemoEndpoints
    {
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);
        private static readonly string rootUrl = ServiceConfig.LoadRequiredString("RootUrl");

        internal static void RequestPost(HttpContext context)
        {
            /* Load request body. */
            using var ms = new MemoryStream();
            context.Request.Body.CopyToAsync(ms).Wait();
            var req = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));

            /* Load the request JSON into a request object. */
            var reqParsed = CallerRequest.Parse(req);


            var token = IssuerSession.Run(reqParsed, rootUrl, DownloadVerifyHash);

        }

        private static string DownloadVerifyHash(Uri url)
        {
            HttpClient http = new HttpClient();
            var result = http.GetAsync(url.ToString()).Result;
            var resultBody = result.Content.ReadAsStringAsync().Result;
            return resultBody.Trim();
        }
    }
}
