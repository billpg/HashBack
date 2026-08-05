using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using billpg.HashBackCore;
using Microsoft.OpenApi;
using System.Net;
using DemoService.Services;

namespace DemoService.Controllers;

[ApiController]
[Route("hello")]
public class HelloController : ControllerBase
{
    private readonly ServiceData data;
    private readonly IHttpGetter httpGetter;

    public HelloController(ServiceData data, IHttpGetter httpGetter)
    {
        this.data = data;
        this.httpGetter = httpGetter;
    }

    private const string HashBackCookieName = "HashBackDemoService";

    // GET /hello/
    [HttpGet]
    [Produces("text/plain", "text/html")]
    [SwaggerOperation(Summary = "Hello endpoint",
        Description = "Returns a simple hello message. but only if you've passed HashBack authentication.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Hello message", typeof(string))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Your request did not pass authentication.")]
    public async Task<ActionResult> Get()
    {
        /* Perform HashBack authentication, or check the cookie. */
        string? authHeader = Request.Headers["Authorization"];
        string? cookieValue = Request.Cookies[HashBackCookieName];
        (string? authDomain, bool isCookieValid) = await Authenticate(authHeader, cookieValue);

        /* If authentication succeeded and the cookie was not set, set it now. */
        if (authDomain != null && !isCookieValid)
        {
            Response.Cookies.Append(HashBackCookieName, JWT.Create(authDomain), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        /* If authenticated, return the hello message. */
        if (authDomain != null)
            return Content($"Hello {authDomain}!", "text/plain");

        /* Failed authentication, return a 401 with some text. */
        Response.Headers["WWW-Authenticate"]
            = $"HashBack realm=\"demo.hashback.dev\" set-cookie=\"{HashBackCookieName}\" version=\"BILLPG_DRAFT_4.2,BILLPG_DRAFT_4.1\"";
        Response.StatusCode = 401;
        return Content(HtmlPages.HelloRoot(), "text/html", Encoding.UTF8);
    }

    private async Task<(string? authDomain, bool isCookieValid)> Authenticate(string? authHeader, string? cookieValue)
    {
        /* Check the cookie first. */
        if (!string.IsNullOrEmpty(cookieValue))
        {
            string domainInCookie = JWT.ParseAndValidateReturnSub(cookieValue);
            if (domainInCookie != null)
                return (domainInCookie, true);
        }

        /* If no valid cookie, check the Authorization header. */
        if (!string.IsNullOrEmpty(authHeader))
        {
            HashBackValidator val = new();
            val.RequireHost(data.ConfigServiceHost);
            val.RequireNowWindow(500);
            val.SetSyncIdentifyUser(url => new Uri(url).Host);
            val.OnGetHash = GetHash;
            var authDomain = await val.Validate(authHeader);
            if (authDomain != null)
                return (authDomain, false);
        }

        return (null, false);
    }

    private async Task<string> GetHash(string url)
    {
        var resp = await httpGetter.GetAsync(new Uri(url));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();        
    }
}