using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace billpg.HashBackAspNetCore;

public static class HashBackAuthenticationExtensions
{
    /// <summary>
    /// Registers HashBack as an authentication scheme, under the default scheme name "HashBack".
    /// Configure at minimum options.Policy.RequireHost(...) and options.Policy.SetSyncIdentifyUser(...)
    /// (or the async OnIdentifyUser) before requests can be authenticated.
    /// </summary>
    public static AuthenticationBuilder AddHashBack(
        this AuthenticationBuilder builder,
        Action<HashBackAuthenticationOptions>? configureOptions = null)
        => builder.AddHashBack(HashBackAuthenticationDefaults.AuthenticationScheme, configureOptions);

    /// <summary>Registers HashBack as an authentication scheme, under the supplied scheme name.</summary>
    public static AuthenticationBuilder AddHashBack(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<HashBackAuthenticationOptions>? configureOptions = null)
    {
        /* Register the named HttpClient used by the built-in verification hash fetcher.
         * (Harmless to call AddHttpClient more than once - it's safe to combine with an
         * app's own unrelated AddHttpClient calls.) No auto-redirects, and a short
         * timeout, per the protocol's own recommendations. */
        builder.Services.AddHttpClient(HashBackAuthenticationDefaults.AuthenticationScheme, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        return builder.AddScheme<HashBackAuthenticationOptions, HashBackAuthenticationHandler>(
            authenticationScheme, configureOptions);
    }
}
