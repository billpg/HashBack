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
    internal static class ErrorHandler
    {
        private class RedirectException : Exception
        {
            public string Target { get; }

            public static RedirectException ToTargetUrl(string target)
                => new RedirectException(target);

            private RedirectException(string target) 
                : base("Redirect to " + target)
            {
                this.Target = target;
            }
        }

        private class ResponseException : Exception
        {
            public ResponseException(int statusCode, string contentType, byte[] body)
                : base("ResponseException")
            {
                this.StatusCode = statusCode;
                this.ContentType = contentType;
                this.Body = body;
            }

            public int StatusCode { get; }
            public string ContentType { get; }
            public byte[] Body { get; }
        }

        internal static void Handle(Exception ex, HttpContext context)
        {
            if (ex is RedirectException rex)
                context.Response.Redirect(rex.Target);

            else if (ex is ResponseException respex)
            {
                context.Response.StatusCode = respex.StatusCode;
                context.Response.ContentType = respex.ContentType;
                context.Response.WriteBodyBytes(respex.Body);
            }
        }

        private static readonly UTF8Encoding UTF8NOBOM
            = new UTF8Encoding(false);

        internal static Exception RedirectExceptionToTargetInConfig(string configKey)
            => RedirectException.ToTargetUrl(ServiceConfig.LoadRequiredString(configKey));

        internal static Exception BadRequestExceptionWithText(string message)
            => new ResponseException(400, "text/plain; charset=utf-8", UTF8NOBOM.GetBytes(message));

        internal static Exception BadRequestExceptionWithJson(JObject body)
            => new ResponseException(400, "application/json", UTF8NOBOM.GetBytes(body.ToString()));
    }
}
