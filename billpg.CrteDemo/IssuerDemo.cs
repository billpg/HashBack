using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrteDemo
{
    internal static class IssuerDemo
    {
        private static long UnixNow()
            => (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);

        private static byte[]? TryBase64Decode(string? base64)
        {
            if (base64 is null)
                return null;

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        internal static void TokenCall(HttpContext context)
        {
            (int httpStatus, JObject responseBody) = TokenCallInternal(context);
            context.Response.Json(httpStatus, responseBody);
        } 

        private static (int httpStatus, JObject responseBody) TokenCallInternal(HttpContext context)
        {
            /* Check request size too large before reading. */
            if (context.Request.ContentLength is null || context.Request.ContentLength > 9999)
                return (403, new JObject());

            /* Confirm no Cookies or other auth. */
            if (context.Request.Headers.ContainsKey("Cookie") || context.Request.Headers.ContainsKey("Authorization"))
                return (400, new JObject { ["Message"] = "Must not use Cookie or Authorization headers." });

            /* Load in the request JSON. */
            using var ms = new MemoryStream();
            context.Request.Body.CopyToAsync(ms).Wait();
            JObject req = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));

            /* Check the required properties are all present. */
            var requiredRequestProperties = new List<string>
            {
                "CrossRequestTokenExchange",
                "IssuerUrl",
                "Now",
                "Unus",
                "VerifyUrl"
            };
            foreach (var suppliedProperty in req.Properties())
            {
                if (requiredRequestProperties.Contains(suppliedProperty.Name) == false)
                    return (400, new JObject { ["Message"] = $"Unexpected {suppliedProperty.Name} property." });
                requiredRequestProperties.Remove(suppliedProperty.Name);
            }
            if (requiredRequestProperties.Any())
                return (400, new JObject { ["Message"] = $"Missing {requiredRequestProperties.First()} property." });

            /* Validate the version. */
            var reqVersion = req["CrossRequestTokenExchange"].Value<string>();
            if (reqVersion != "CRTE_PUBLIC_DRAFT_3")
                return (400, new JObject
                {
                    ["Message"] = "Unknown version of exchange.",
                    ["Version"] = new JArray { "CRTE_PUBLIC_DRAFT_3" }
                });

            /* Validate our URL. */
            var expectedIssuerUrl = context.Request.Url();
            var reqIssuerUrl = req["IssuerUrl"].Value<string>();
            if (reqIssuerUrl != expectedIssuerUrl)
                return (400, new JObject { ["IssuerUrl"] = expectedIssuerUrl });

            /* Validate Now. */
            var now = UnixNow();
            var reqNow = req["Now"].Value<long>();
            if (now - reqNow > 60)
                return (400, new JObject { ["Now"] = now });

            /* Validate Unus. */
            var reqUnus = req["Unus"].ValueNotNull<string>();
            byte[]? unusBytes = TryBase64Decode(reqUnus);
            if (unusBytes is null || unusBytes.Length != 256/8)
                return (400, new JObject { ["Message"] = "Bad Unus property. Must be 256 bits as base64." });



        }

    }
}
