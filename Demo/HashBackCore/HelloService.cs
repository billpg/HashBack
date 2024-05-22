using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class HelloService
    {
        public Uri RootUrl { get; set; } = null!;
        public OnAuthorizationHeaderFn OnAuthorizationHeader { get; set; }
            = header => throw new NotImplementedException();

        public void ConfigureHttpService(WebApplication app, string path)
        {
            app.MapGet(path, GetHandler);
        }

        private IResult GetHandler(
            HttpContext context, 
            [FromHeader(Name = "Authorization")] string? authHeader)
        {
            /* Respond to queries without an Authorizaton header with a 401. */
            if (string.IsNullOrEmpty(authHeader))
                return BearerTokenService.Set401Response(context);

            /* Check the Auth header. Will throw a 400 exception if not valid. */
            (string subject, long serverNow) = OnAuthorizationHeader(authHeader);

            /* Return a personalised message. */
            return Results.Text(
                $"Hello everyone at {subject}.\r\n" +
                "Hope you're having a lovely day!");
        }
    }
}
