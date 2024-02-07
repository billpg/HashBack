using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
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

        internal static readonly IReadOnlyDictionary<string,string> ContentTypes = new Dictionary<string, string>
        {
            {"css", "text/css" }
        };

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

        private static byte[] ObjectToBytes(object? value)
        {
            /* Shortcut nulls to an empty string. */
            if (value is null || value is DBNull)
                return new byte[] { };

            /* UTF8 encode strings. */
            if (value is string valueAsString)
                return UTF8.GetBytes(valueAsString);

            /* Otherwise, complain about unknown type. */
            throw new ApplicationException(
                $"Don't know how to convert {value.GetType().FullName} into bytes.");
        }

        internal static void MapResources(this WebApplication app, string folder, Type resourcesType)
        {
            /* Load the supplied resources type into a dictionary of strings. */
            var props =
                resourcesType
                .GetProperties(
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)
                .Where(prop => prop.PropertyType == typeof(string))
                .Where(prop => prop.Name.Contains('_'));

            /* Loop through them and map them. */
            foreach (var prop in props)
            {
                /* Split name into parts. */
                var nameSplit = prop.Name.Split('_');
                string assetName = nameSplit[0] + "." + nameSplit[1];
                string contentType = ContentTypes[nameSplit[1]];

                /* Map asset. */
                MapGetBytes(
                    app, 
                    folder + "/" + assetName, 
                    contentType, 
                    ObjectToBytes(prop.GetValue(null)));
            }            
        }

        internal static void StringResponse(this HttpResponse response, int httpStatusCode, string contentType, string responseBody)
        {
            response.StatusCode = httpStatusCode;
            response.ContentType = contentType;
            response.Body.Write(Encoding.UTF8.GetBytes(responseBody));
        }

        internal static void Json(this HttpResponse response, int httpStatusCode, JObject body)
            => StringResponse(response, httpStatusCode, "application/json", body.ToString());

        internal static string Url(this HttpRequest req)
            => $"{req.Scheme}://{req.Host}/{req.Path}?{req.QueryString}";

        

        internal static T ValueNotNull<T>(this JToken j)
        {
            T? tryValue = j.Value<T>();
            if (tryValue is null)
                throw new ApplicationException("Missing property in JSON.");
            return tryValue;
        }
    }
}
