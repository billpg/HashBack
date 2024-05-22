using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Linq.Expressions;

namespace billpg.HashBackCore
{
    public class BearerTokenService
    {
        public Uri RootUrl { get; set; } = null!;
        public int NowValidationMarginSeconds { get; set; } = 10;
        public int IssuedTokenLifespanSeconds { get; set; } = 1000;
        public OnErrorFn OnBadRequest { get; set; }
            = msg => throw new NotImplementedException();
        public OnReadClockFn OnReadClock { get; set; }
            = () => throw new NotImplementedException();
        public OnRetrieveVerifyHashFn OnRetrieveVerifyHash { get; set; }
            = url => throw new NotImplementedException();
        public OnAuthorizationHeaderFn OnAuthorizationHeader { get; set; }
            = header => throw new NotImplementedException();

        public void ConfigureHttpService(WebApplication app, string path)
        {
            app.MapGet(path, this.GetHandler);
        }

        private IResult GetHandler(
            HttpContext context,
            [FromHeader(Name = "Authorization")] string? authHeader)
        {
            /* Respond to queries without an Authorizaton header with a 401. */
            if (string.IsNullOrEmpty(authHeader))
                return Set401Response(context);

            /* Handle the Authorization header. This will either throw or return the 
             * validated subject. */
            (string subject, long serverNow) = OnAuthorizationHeader(authHeader);

            /* They match. Return an issued bearer token. */
            long expiresAt = serverNow + IssuedTokenLifespanSeconds;
            string jwt = JWT.Build(RootUrl.Host, subject, serverNow, expiresAt);
            return IssuerService.IssueBearerTokenResult(serverNow, expiresAt, jwt);
        }

        internal static IResult Set401Response(HttpContext context)
        {
            context.Response.Headers.Append("WWW-Authenticate", "HashBack");
            context.Response.Headers.Append("WWW-Authenticate", "Bearer");
            return Results.Text(
                content: "Missing Authorization header.",
                statusCode: 401);
        }
    }
}
