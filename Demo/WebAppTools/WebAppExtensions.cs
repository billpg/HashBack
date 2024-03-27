using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace billpg.WebAppTools
{
    public interface IHandlerProxy
    {
        string? RequestParam(string key);
        Uri RequestUrl { get; }
        string? RequestHeader(string name);
        byte[]? RequestBody { get; }

        void ResponseCode(int code);
        void ResponseHeader(string name, string value);
        void ResponseBody(byte[] body);
        void ResponseRedirect(string target);
        void ResponseAddCookie(string name, string value, CookieOptions opts);
    }


    public static class WebAppExtensions
    {
        private const string applicationJson = "application/json";
        private static readonly Func<string, byte[]> Utf8GetBytes 
            = new UTF8Encoding(false).GetBytes;
        private static readonly JsonSerializerOptions ToStringIndented 
            = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions ToStringOneLine
            = new JsonSerializerOptions { WriteIndented = false };        

        public delegate void WebHandler(IHandlerProxy proxy);

        public static void MapGetWrapped(this WebApplication app, string pattern, WebHandler handler)
            => app.MapGet(pattern, Wrap(handler));

        public static void MapPostWrapped(this WebApplication app, string pattern, WebHandler handler)
            => app.MapPost(pattern, Wrap(handler));

        private static Delegate Wrap(WebHandler handler)
        {
            return Wrapper;
            void Wrapper(HttpContext context)
            {
                /* Set up proxy around context. */
                IHandlerProxy proxy = new ProxyForHttpContext(context);

                try
                {
                    /* Call through to the wrapped context. */
                    handler(proxy);
                }
                catch (HttpResponseException hrex)
                {
                    /* This is the special HTTP response exception, use
                     * it to build the response itself. */
                    proxy.ResponseCode(hrex.StatusCode);
                    foreach (var header in hrex.Headers)
                        proxy.ResponseHeader(header.Key, header.Value);
                    proxy.ResponseJson(hrex.ResponseBody);
                }
            }
        }

        private class ProxyForHttpContext : IHandlerProxy
        {
            private HttpContext context;

            internal ProxyForHttpContext(HttpContext context)
            {
                this.context = context;
            }

            Uri IHandlerProxy.RequestUrl => context.Request.Url();

            byte[]? IHandlerProxy.RequestBody
            {
                get
                {
                    /* Load the full request into memeory, waiting until it has completed. */
                    using var ms = new MemoryStream();
                    context.Request.Body.CopyToAsync(ms).Wait();

                    /* Return the loaded bytes. */
                    return ms.ToArray();
                }
            }

            string? IHandlerProxy.RequestHeader(string name)
            {
                if (context.Request.Headers.TryGetValue(name, out var headers))
                    return headers.FirstOrDefault();
                return null;
            }

            string? IHandlerProxy.RequestParam(string key)
            {
                if (context.Request.Query.TryGetValue(key, out var values))
                    return values.FirstOrDefault();

                return null;
            }

            void IHandlerProxy.ResponseAddCookie(string name, string value, CookieOptions opts)
                => context.Response.Cookies.Append(name, value, opts);
            
            void IHandlerProxy.ResponseBody(byte[] body)
                => context.Response.Body.WriteAsync(body);

            void IHandlerProxy.ResponseCode(int code)
                => context.Response.StatusCode = code;

            void IHandlerProxy.ResponseHeader(string name, string value)
                => context.Response.Headers[name] = value;

            void IHandlerProxy.ResponseRedirect(string target)
                => context.Response.Redirect(target);
        }

        public static JObject? RequestJson(this IHandlerProxy proxy)
        {
            byte[]? requestAsBytes = proxy.RequestBody;
            if (requestAsBytes == null)
                return null;
            string requestAsString = Encoding.UTF8.GetString(requestAsBytes);
            return JObject.Parse(requestAsString);
        }

        public static void ResponseText(this IHandlerProxy proxy, string text)
        {
            proxy.ResponseHeader("Content-Type", "text/plain");
            proxy.ResponseBody(Utf8GetBytes(text));
        }

        public static void ResponseJson(this IHandlerProxy proxy, JToken node)
        {
            proxy.ResponseHeader("Content-Type", "application/json");
            proxy.ResponseBody(Utf8GetBytes(node.ToString(Newtonsoft.Json.Formatting.Indented)));
        }


        public static Uri Url(this HttpRequest req)
        {
            string protocol = req.IsHttps ? "https" : "http";
            return new Uri($"{protocol}://{req.Host}{req.Path}");
        }

        public static Uri WithRelativePath(this Uri baseUrl, string relativePath)
            => new Uri(baseUrl, relativePath);

        public static void WriteBodyJson(this HttpResponse response, JToken body)
        {
            response.ContentType = "application/json";
            response.WriteBodyString(body.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        public static void WriteBodyString(this HttpResponse response, string bodyText)
        {
            response.WriteBodyBytes(Utf8GetBytes(bodyText));
        }

        public static void WriteBodyBytes(this HttpResponse response, byte[] bodyAsBytes)
        {
            response.Body.WriteAsync(bodyAsBytes);
        }

    }
}
