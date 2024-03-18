using billpg.HashBackCore;
using billpg.WebAppTools;
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

            /* Run the exchange, supplying a callback that'll 
             * download the verification hash. */
            var token = IssuerSession.Run(reqParsed, rootUrl, DownloadVerifyHash);

            /* Pass control to populate the response to the specific
             * requested handler. */
            if (reqParsed.TypeOfResponse == "BearerToken")
                PopulateResponseBearerToken(context, token);
            else if (reqParsed.TypeOfResponse == "JWT")
                PopulateResponseJWT(context, token.JWT);
            else if (reqParsed.TypeOfResponse == "204SetCookie")
                PopulateResponse204SetCookie(context, token);
            else
                throw new BadRequestException(
                    "Unknown TypeOfRsponse. Expected BearerToken/JWT/204SetCookie.")
                    .WithResponseProperty(
                        "AcceptTypeOfResponse",
                        new JArray { "BearerToken", "JWT", "204SetCookie" });
        }

        private static void PopulateResponseBearerToken(HttpContext context, IssuerSession.IssuedToken token)
        {
            JObject responseBody = new JObject 
            {
                ["BearerToken"] = token.JWT,
                ["IssuedAt"] = token.IssuedAt,
                ["ExpiresAt"] = token.ExpiresAt
            };

            context.Response.StatusCode = 200;
            context.Response.WriteBodyJson(responseBody);
        }

        private static void PopulateResponseJWT(HttpContext context, string jwt)
        {
            context.Response.StatusCode = 200;
            context.Response.WriteBodyJson(JValue.CreateString(jwt));
        }

        private static void PopulateResponse204SetCookie(HttpContext context, IssuerSession.IssuedToken token)
        {
            /* Build cookie options from the token's expiry. */
            CookieOptions opts = new CookieOptions 
            {
                HttpOnly = true, 
                Secure = true, 
                Expires = DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt) 
            };

            /* Set response. */
            context.Response.Cookies.Append("HashBack_JWT", token.JWT, opts);
            context.Response.StatusCode = 204;
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
