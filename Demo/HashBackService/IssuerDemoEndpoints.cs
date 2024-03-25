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

        internal static void RequestPost(IHandlerProxy proxy)
        {
            /* Load request body. */
            var req = proxy.RequestJson();
            if (req == null)
                throw new BadHttpRequestException("Missing request body.");

            /* Load the request JSON into a request object. */
            var reqParsed = CallerRequest.Parse(req);

            /* Run the exchange, supplying a callback that'll 
             * download the verification hash. */
            var token = IssuerSession.Run(reqParsed, rootUrl, DownloadVerifyHash);

            /* Pass control to populate the response to the specific
             * requested handler. */
            if (reqParsed.TypeOfResponse == "BearerToken")
                PopulateResponseBearerToken(proxy, token);
            else if (reqParsed.TypeOfResponse == "JWT")
                PopulateResponseJWT(proxy, token.JWT);
            else if (reqParsed.TypeOfResponse == "204SetCookie")
                PopulateResponse204SetCookie(proxy, token);
            else
                throw new BadRequestException(
                    "Unknown TypeOfRsponse. Expected BearerToken/JWT/204SetCookie.")
                    .WithResponseProperty(
                        "AcceptTypeOfResponse",
                        new JArray { "BearerToken", "JWT", "204SetCookie" });
        }

        private static void PopulateResponseBearerToken(IHandlerProxy proxy, IssuerSession.IssuedToken token)
        {
            JObject responseBody = new JObject 
            {
                ["BearerToken"] = token.JWT,
                ["IssuedAt"] = token.IssuedAt,
                ["ExpiresAt"] = token.ExpiresAt
            };

            proxy.ResponseCode(200);
            proxy.ResponseJson(responseBody);
        }

        private static void PopulateResponseJWT(IHandlerProxy proxy, string jwt)
        {
            proxy.ResponseCode(200);
            proxy.ResponseJson(JValue.CreateString(jwt));
        }

        private static void PopulateResponse204SetCookie(IHandlerProxy proxy, IssuerSession.IssuedToken token)
        {
            /* Build cookie options from the token's expiry. */
            CookieOptions opts = new CookieOptions 
            {
                HttpOnly = true, 
                Secure = true, 
                Expires = DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt) 
            };

            /* Set response. */
            proxy.ResponseCode(204);
            proxy.ResponseAddCookie("HashBack_JWT", token.JWT, opts);
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
