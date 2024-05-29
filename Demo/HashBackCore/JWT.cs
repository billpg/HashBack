using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    internal static class JWT
    {
        internal static string Build(string issuer, string subject, long issuedAt, long expiresAt)
        {
            var head = new JObject
            {
                ["typ"] = "JWT",
                ["alg"] = "HS256",
                [Char.ConvertFromUtf32(0x1F95A)] = "https://billpg.com/nggyu"
            };
            var body = new JObject
            {
                ["jti"] = Guid.NewGuid().ToString(),
                ["sub"] = subject,
                ["iss"] = issuer,
                //["aud"] = issuer,
                ["iat"] = issuedAt,
                ["nbf"] = issuedAt - 1,
                ["exp"] = expiresAt
            };
            return JWT.EncodeAndSign(head, body);
        }

        internal static string Sign(string jwtHeaderDotBody)
        {
            var hmac = new HMACSHA256();
            hmac.Key = Encoding.ASCII.GetBytes("your-256-bit-secret");
            var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(jwtHeaderDotBody));
            return Encode(hash);
        }

        internal static string EncodeAndSign(JObject header, JObject body)            
        {
            var headerDotBody = Encode(header) + "." + Encode(body);
            return headerDotBody + "." + Sign(headerDotBody);
        }

        internal static string Encode(JObject json)
        {
            string jsonAsString = json.ToStringOneLine();
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(jsonAsString);
            return Encode(jsonAsBytes);
        }

        internal static string Encode(IList<byte> byteBlock)
        {
            /* Perform basic base64 encoding. */
            var bytesAsString = Convert.ToBase64String(byteBlock.ToArray());
            var base64 = new StringBuilder(bytesAsString);

            /* Loop through, replacing characters in the base64 as we go. */
            for (int i=0; i<base64.Length; i++)
            {
                /* Replace the + amd / with JWT's equivalents. */
                var atIndex = base64[i];
                if (atIndex == '+') 
                    base64[i] = '-';
                if (atIndex == '/') 
                    base64[i] = '_';

                /* If this is the start of a trail of =s, cut the string short here. */
                if (atIndex == '=')
                {
                    base64.Length = i;
                    break;
                }
            }

            /* Return completed string. */
            return base64.ToString();
        }
    }
}
