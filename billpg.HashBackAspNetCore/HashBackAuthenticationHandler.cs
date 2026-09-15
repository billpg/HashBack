using billpg.HashBackCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace billpg.HashBackAspNetCore;

/// <summary>
/// An ASP.NET Core authentication handler for the HashBack protocol. Register it via
/// <see cref="HashBackAuthenticationExtensions.AddHashBack(AuthenticationBuilder, System.Action{HashBackAuthenticationOptions}?)"/>.
/// </summary>
public class HashBackAuthenticationHandler : AuthenticationHandler<HashBackAuthenticationOptions>
{
    private readonly IHttpClientFactory httpClientFactory;

    public HashBackAuthenticationHandler(
        IOptionsMonitor<HashBackAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory)
        : base(options, logger, encoder)
    {
        this.httpClientFactory = httpClientFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        /* No Authorization header, or not a HashBack one - let other handlers (or an
         * eventual 401 challenge) take it from here. */
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith(HashBackAuthenticationDefaults.AuthenticationScheme,
                StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        /* Unless the caller has opted out, use the built-in fetcher for downloading
         * verification hashes. */
        if (Options.UseDefaultVerificationHashFetcher)
            Options.Policy.OnGetVerificationHash = FetchVerificationHashAsync;

        /* Parse and authenticate against the configured policy. */
        string user;
        try
        {
            user = await HashBackRequest.Authenticate(authHeader, Options.Policy);
        }
        catch (AuthorizationParseException ex)
        {
            Logger.LogInformation(ex, "HashBack authentication rejected: {Reason}", ex.Reason);
            return AuthenticateResult.Fail(ex);
        }

        /* Build the resulting principal from the identified user. */
        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user));
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, Scheme.Name));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Issues the WWW-Authenticate challenge header described in the HashBack specification,
    /// alongside the usual 401 response.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        string challenge = Options.Realm != null
            ? $"{HashBackAuthenticationDefaults.AuthenticationScheme} realm=\"{Options.Realm}\""
            : HashBackAuthenticationDefaults.AuthenticationScheme;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
        return base.HandleChallengeAsync(properties);
    }

    /// <summary>
    /// The built-in verification hash fetcher: a plain HTTPS GET request with a short timeout
    /// and no automatic redirects (per the protocol's requirements). Apps with stricter security
    /// needs - such as SSRF protections or IP allow-listing - should set
    /// <see cref="HashBackAuthenticationOptions.UseDefaultVerificationHashFetcher"/> to false and
    /// assign their own <see cref="HashBackPolicy.OnGetVerificationHash"/> instead.
    /// </summary>
    private async Task<string> FetchVerificationHashAsync(Uri verify)
    {
        var client = httpClientFactory.CreateClient(HashBackAuthenticationDefaults.AuthenticationScheme);
        using var response = await client.GetAsync(verify);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();
        return body.Trim();
    }
}
