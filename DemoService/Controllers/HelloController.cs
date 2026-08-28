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

    /// <summary>
    /// Clock-drift tolerance for the Now property, and (since it must be at least as wide
    /// as that tolerance to be effective) the window Unus values are remembered for replay
    /// protection.
    /// </summary>
    private const int NowToleranceSeconds = 500;

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
        string? authHeader = Request.Headers.Authorization;
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
                Expires = DateTimeOffset.UtcNow.Add(JWT.Lifetime)
            });
        }

        /* If authenticated, return the hello message. */
        if (authDomain != null)
            return Content($"Hello {authDomain}!", "text/plain");

        /* Failed authentication, return a 401 with some text. */
        Response.Headers["WWW-Authenticate"]
            = $"HashBack realm=\"demo.hashback.dev\"" +
            $" set-cookie=\"{HashBackCookieName}\"" +
            $" version=\"BILLPG_DRAFT_4.2,BILLPG_DRAFT_4.1\"";
        Response.StatusCode = 401;
        return Content(HtmlPages.HelloRoot(), "text/html", Encoding.UTF8);
    }

    private async Task<(string? authDomain, bool isCookieValid)> Authenticate(string? authHeader, string? cookieValue)
    {
        /* Check the cookie first. If its valid, return the domain inside it. */
        if (!string.IsNullOrEmpty(cookieValue))
        {
            string? domainInCookie = JWT.ParseAndValidateReturnSub(cookieValue);
            if (domainInCookie != null)
                return (domainInCookie, true);
        }

        /* If no valid cookie, check the Authorization header. */
        if (!string.IsNullOrEmpty(authHeader))
        {
            HashBackPolicy policy = new();
            policy.RequireHost(data.ConfigServiceHost);
            policy.RequireNowWindow(NowToleranceSeconds);
            policy.SetSyncUnusValidate(unus => data.TryRecordUnus(unus, TimeSpan.FromSeconds(NowToleranceSeconds)));
            policy.SetSyncIdentifyUser(verify => verify.Host);
            policy.OnGetVerificationHash = verify => GetHash(verify.ToString());
            var authDomain = await HashBackRequest.Authenticate(authHeader, policy);
            if (authDomain != null)
                return (authDomain, false);
        }

        /* Not authenticated. */
        return (null, false);
    }

    private async Task<string> GetHash(string url)
    {
        /* Call the supplied verification URL and get the results. */
        var resp = await httpGetter.GetAsync(new SimpleHttpRequest(url));
        if (resp.StatusCode != 200)
            throw new BadRequestException("Bad Verification URL.", 
                $"{url} returned status code {resp.StatusCode}");

        /* Split the response into tokens, looking for the first one that decodes to 256
         * bits. This will allow chunked to work, as the length-of-chunk line will be
         * ignored. The comparison against the expected hash happens on the decoded bytes -
         * by re-encoding here to standard base64 - rather than on this raw text, so a hash
         * published using the alternate hyphen/underscore (base64url) form is still
         * accepted. */
        foreach (string token in resp.Body.Split(" \r\n\t".ToCharArray()))
        {
            byte[]? hashBytes = Helpers.TryParseBase64(token, 256 / 8);
            if (hashBytes != null)
                return Convert.ToBase64String(hashBytes);
        }

        /* No lines fit. */
        throw new BadRequestException("Bad Verification Hash",
            $"{url} did not return a suitable base-64 verification hash.");
    }
}