using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace billpg.WebAppTools
{
    public static class WebAppExtensions
    {
        private const string applicationJson = "application/json";
        private static readonly Func<string, byte[]> Utf8GetBytes 
            = new UTF8Encoding(false).GetBytes;
        private static readonly JsonSerializerOptions ToStringIndented 
            = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions ToStringOneLine
            = new JsonSerializerOptions { WriteIndented = false };

        public delegate void WebHandler(HttpContext context);

        public static void MapGetRedirectTo(this WebApplication app, string pattern, string redirectTo)
            => app.MapGetWrapped(pattern, context => context.Response.Redirect(redirectTo));

        public static void MapGetWrapped(this WebApplication app, string pattern, WebHandler handler)
            => app.MapGet(pattern, Wrap(handler));

        public static void MapPostWrapped(this WebApplication app, string pattern, WebHandler handler)
            => app.MapPost(pattern, Wrap(handler));

        private static WebHandler Wrap(WebHandler handler)
        {
            return Wrapper;
            void Wrapper(HttpContext context)
            {
                try
                {
                    /* Call through to the wrapped context. */
                    handler(context);
                }
                catch (HttpResponseException hrex)
                {
                    /* Convert the response body to an array of bytes. */
                    var bodyAsString = hrex.ResponseBody.ToString();
                    var bodyAsBytes = Utf8GetBytes(bodyAsString);

                    /* Set up the response as instructed. */
                    context.Response.StatusCode = hrex.StatusCode;
                    context.Response.ContentType = applicationJson;
                    foreach (var header in hrex.Headers)
                        context.Response.Headers[header.Key] = header.Value;
                    context.Response.Body.WriteAsync(bodyAsBytes);
                }
            }
        }
    }
}
