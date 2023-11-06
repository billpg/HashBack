using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrteDemo
{
    internal static class WebAppExtensions
    {
        internal static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        internal static void MapGetBytes(this WebApplication app, string pattern, string contentType, byte[] bytes)
        {
            app.MapGet(pattern, GetBytesFromGetterHandler(Getter));
            (string contentType, byte[] bytes) Getter()
                => (contentType, bytes);
        }

        internal static void MapGetBytes(this WebApplication app, string pattern, Func<(string contentType, byte[] bytes)> getter)
        {
            app.MapGet(pattern, GetBytesFromGetterHandler(getter));
        }

        internal static void MapGetBytes(this WebApplication app, string pattern, string contentType, Func<byte[]> getter)
        {
            app.MapGet(pattern, GetBytesFromGetterHandler(Getter));
            (string contentType, byte[] bytes) Getter()
                => (contentType, getter());
        }

        internal static void MapGetHtml(this WebApplication app, string pattern, string html)
            => MapGetBytes(app, pattern, "text/html", UTF8.GetBytes(html));

        private static Action<HttpContext> GetBytesFromGetterHandler(Func<(string contentType, byte[] bytes)> getter)
        {
            return Internal;
            void Internal(HttpContext context)
            {
                (string contentType, byte[] bytes) = getter();
                context.Response.ContentType = contentType;
                context.Response.Body.WriteAsync(bytes);
            }
        }


    }
}
