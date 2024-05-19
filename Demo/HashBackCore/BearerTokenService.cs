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

        public void ConfigureHttpService(WebApplication app, string path)
        {
            app.MapGet(path, this.GetHandler);
        }

        private IResult GetHandler(
            [FromHeader(Name = "Authorization")] string? authHeader,
            [FromHeader(Name = "Accept")] string? acceptHeader,
            HttpContext context)
        {
            /* Respond to queries without an Authorizaton header with a 401. */
            if (string.IsNullOrEmpty(authHeader))
            {
                context.Response.Headers.Append("WWW-Authenticate", "HashBack");
                return Results.Text(content: "Missing Authorization header.", statusCode: 401);
            }

            /* If Accept header is missing, add default. */
            if (string.IsNullOrEmpty(acceptHeader))
                acceptHeader = "text/plain";

            /* Pull out the remote IP and check block-list. */
            IPAddress remoteIp = context.RemoteIP();
            //TODO: Check the block list.

            /* Handle the Authorization header. This will either throw or return the 
             * validated subject. */
            long serverNow = OnReadClock();
            string validatedVerifyDomain 
                = AuthorizationHeader.Handle(
                    authHeader, 
                    this.OnBadRequest,
                    this.OnRetrieveVerifyHash, 
                    serverNow, 
                    this.RootUrl, 
                    this.NowValidationMarginSeconds);

            /* They match. Return an issued bearer token. */
            long expiresAt = serverNow + IssuedTokenLifespanSeconds;
            string jwt = IssuerService.BuildJWT(RootUrl.Host, validatedVerifyDomain, serverNow, expiresAt);
            return IssuerService.IssueBearerTokenResult(serverNow, expiresAt, jwt);
        }
    }
}
