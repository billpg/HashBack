using billpg.HashBackCore;
using Microsoft.AspNetCore.Authentication;

namespace billpg.HashBackAspNetCore;

public class HashBackAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The policy applied to every incoming request. At minimum, configure
    /// RequireHost/RequireAnyHost and OnIdentifyUser (or SetSyncIdentifyUser)
    /// before use - HashBack will refuse every request while these are left
    /// at their default, unconfigured state. RequireUnusNotReused is also
    /// recommended, for replay protection.
    /// </summary>
    public HashBackPolicy Policy { get; } = new();

    /// <summary>The optional realm parameter to include in the WWW-Authenticate challenge header.</summary>
    public string? Realm { get; set; }

    /// <summary>
    /// If true (the default), Policy.OnGetVerificationHash is set to a built-in
    /// HttpClient-based fetcher before every authentication attempt, overwriting
    /// anything you assigned yourself. Set this to false and configure
    /// Policy.OnGetVerificationHash (or SetSyncGetVerificationHash) yourself for
    /// tighter control - for example, to add SSRF protections such as IP
    /// allow-listing, as billpg's own demo service does.
    /// </summary>
    public bool UseDefaultVerificationHashFetcher { get; set; } = true;
}
