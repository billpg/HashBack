using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using billpg.HashBackCore;
using System.Net;
using DemoService.Data;
using DemoService.Services;
using billpg.WWWAuthenticateTools;

namespace DemoService.Controllers;

[ApiController]
[Route("hello")]
public class HelloController : ControllerBase
{
    private readonly ServiceData data;
    private readonly IHttpGetter httpGetter;
    private readonly IHelloRequestLog requestLog;

    public HelloController(ServiceData data, IHttpGetter httpGetter, IHelloRequestLog requestLog)
    {
        this.data = data;
        this.httpGetter = httpGetter;
        this.requestLog = requestLog;
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
        (string? authDomain, bool isCookieValid, bool isBlocked) = await Authenticate(authHeader, cookieValue);

        /* This caller IP has too many recent failures - turn it away without having done
         * any of the real (expensive) work. */
        if (isBlocked)
        {
            Response.Headers["Retry-After"] = ((int)HelloRequestLog.FailureLookbackWindow.TotalSeconds).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                "Too many recent failed authentication attempts from this caller. Try again later.");
        }

        /* If authentication succeeded and the cookie was not set, set it now. */
        if (authDomain != null && !isCookieValid)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.Add(JWT.Lifetime)
            };
            options.Extensions.Add("Auth-Scheme=HashBack");
            options.Extensions.Add("Auth-Realm=demo.hashback.dev");
            Response.Cookies.Append(HashBackCookieName, JWT.Create(authDomain), options);
        }

        /* If authenticated, return the hello message. */
        if (authDomain != null)
            return Content($"Hello {authDomain}! ({(isCookieValid?"Cookie":"HashBack")} validated.)", "text/plain");

        /* Failed authentication, return a 401 with some text. */
        var wwwAuthHeader = new AuthHeaders()
            .WithScheme("HashBack")
            .WithParam("realm", "demo.hashback.dev")
            .WithParam("version", "BILLPG_DRAFT_4.2,BILLPG_DRAFT_4.1")
            .WithScheme("Cookie")
            .WithParam("realm", "demo.hashback.dev")
            .WithParam("name", HashBackCookieName);
        Response.Headers["WWW-Authenticate"] = wwwAuthHeader.ToSingleHeaderValue();
        Response.StatusCode = 401;
        return Content(HtmlPages.HelloRoot(), "text/html", Encoding.UTF8);
    }

    private async Task<(string? authDomain, bool isCookieValid, bool isBlocked)> Authenticate(string? authHeader, string? cookieValue)
    {
        /* Check the cookie first. If its valid, return the domain inside it. */
        if (!string.IsNullOrEmpty(cookieValue))
        {
            string? domainInCookie = JWT.ParseAndValidateReturnSub(cookieValue);
            if (domainInCookie != null)
                return (domainInCookie, true, false);
        }

        /* If no valid cookie, check the Authorization header - and log the attempt,
         * whatever it turns out to be, for later abuse-pattern review. */
        if (!string.IsNullOrEmpty(authHeader))
        {
            IPAddress callerIp = Request.RequestIP();
            var authDomain = await AuthenticateHashBack(authHeader, callerIp);
            if (authDomain != null)
                return (authDomain, false, false);
        }

        /* Not authenticated. */
        return (null, false, false);
    }

    private async Task<string?> AuthenticateHashBack(string authHeader, IPAddress callerIp)
    {
        HashBackRequest? claim = null;
        IPAddress? verificationIp = null;
        string? authDomain = null;
        HelloRequestOutcome outcome = HelloRequestOutcome.UnexpectedError;
        string? detail = null;

        try
        {
            /* Parsed separately from Authenticate (rather than the combined static
             * Authenticate(header, policy) helper) so the claim's fields are available for
             * logging even if authentication itself goes on to fail. */
            claim = HashBackRequest.Parse(authHeader);

            HashBackPolicy policy = new();
            policy.RequireHost(data.ConfigServiceHost);
            policy.RequireNowWindow(NowToleranceSeconds);
            policy.SetSyncUnusValidate(unus => data.TryRecordUnus(unus, TimeSpan.FromSeconds(NowToleranceSeconds)));
            policy.SetSyncIdentifyUser(verify => verify.Host);
            policy.OnGetVerificationHash = BuildGetHash(callerIp, ip => verificationIp = ip);

            authDomain = await claim.Authenticate(policy);
            outcome = HelloRequestOutcome.Success;
            return authDomain;
        }
        catch (AuthorizationParseException ex)
        {
            outcome = MapOutcome(ex.Reason);
            detail = ex.Message;
            throw;
        }
        catch (Exception ex) when (ex is BadRequestException or ApplicationException)
        {
            /* Everything HttpGetter/GetHash can throw once past parsing and policy checks -
             * unreachable, timed out, TLS rejected, bad status, filtered IP, and so on. */
            outcome = HelloRequestOutcome.VerificationFetchFailed;
            detail = ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            outcome = HelloRequestOutcome.UnexpectedError;
            detail = ex.Message;
            throw;
        }
        finally
        {
            await requestLog.LogAsync(callerIp, claim, verificationIp, outcome, detail);
        }
    }

    private static HelloRequestOutcome MapOutcome(ValidateRejectionReason reason) => reason switch
    {
        ValidateRejectionReason.BadHeader => HelloRequestOutcome.BadHeader,
        ValidateRejectionReason.WrongHost => HelloRequestOutcome.WrongHost,
        ValidateRejectionReason.WrongNow => HelloRequestOutcome.WrongNow,
        ValidateRejectionReason.ReplayedUnus => HelloRequestOutcome.ReplayedUnus,
        ValidateRejectionReason.UnknownUser => HelloRequestOutcome.UnknownUser,
        ValidateRejectionReason.WrongHash => HelloRequestOutcome.WrongHash,
        _ => HelloRequestOutcome.UnexpectedError
    };


    private HashBackPolicy.GetVerificationHashDelegate BuildGetHash(IPAddress callerIp, Action<IPAddress> storeVerificationIp)
    {
        /* Return a delegate that fits the OnGetVeificationHash, but also
         * calls the supplied action to save the verification IP address too. */
        return InternalGetHash;
        async Task<string> InternalGetHash(Uri url)
        {
            /* Call the supplied verification URL and get the results. */
            var resp = await httpGetter.GetAsync(url, null, callerIp, OutboundGetSource.Hello);
            if (resp.StatusCode != 200)
                throw new BadRequestException("Bad Verification URL.",
                    $"{url} returned status code {resp.StatusCode}");
            storeVerificationIp(resp.RemoteAddress!);
            
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
}
