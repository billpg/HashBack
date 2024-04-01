using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackService
{
    internal static class VerificationHashDownload
    {
        internal static string Retrieve(Uri url)
        {
            HttpClient http = new HttpClient();
            var result = http.GetAsync(url.ToString()).Result;
            var resultBody = result.Content.ReadAsStringAsync().Result;
            return resultBody.Trim();
        }
    }
}
