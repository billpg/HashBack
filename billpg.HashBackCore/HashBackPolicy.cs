using System;
using System.Threading.Tasks;

namespace billpg.HashBackCore;

/// <summary>
/// The pluggable set of rules a server applies when authenticating an incoming HashBack
/// request via <see cref="HashBackRequest.Authenticate(HashBackPolicy)"/>. Configure the
/// delegate properties directly, or use the helper extension methods in
/// <see cref="HashBackPolicyExtensions"/> for the common cases.
/// </summary>
public class HashBackPolicy
{
    public delegate Task<bool> HostValidateDelegate(string host);
    public delegate Task<bool> NowValidateDelegate(long now);
    public delegate Task<bool> UnusValidateDelegate(string unus);
    public delegate Task<string?> IdentifyUserDelegate(Uri verify);
    public delegate Task<string> GetVerificationHashDelegate(Uri verify);
    public delegate void LogWriteDelegate(string logText);

    /// <summary>Checks the request's Host property is one this server recognizes as itself.</summary>
    public HostValidateDelegate OnHostValidate { get; set; }
        = _ => throw new NotImplementedException(
            $"{nameof(OnHostValidate)} not configured. See {nameof(HashBackPolicyExtensions.RequireHost)}.");

    /// <summary>Checks the request's Now property is close enough to this server's own clock.</summary>
    public NowValidateDelegate OnNowValidate { get; set; }
        = _ => throw new NotImplementedException(
            $"{nameof(OnNowValidate)} not configured. See {nameof(HashBackPolicyExtensions.RequireNowWindow)}.");

    /// <summary>
    /// Checks the request's Unus property has not been seen before. The default accepts any
    /// well-formed value - the 128-bit/BASE-64 format itself is already checked while parsing
    /// the header. See <see cref="HashBackPolicyExtensions.RequireUnusNotReused"/> to configure
    /// replay protection.
    /// </summary>
    public UnusValidateDelegate OnUnusValidate { get; set; }
        = _ => Task.FromResult(true);

    /// <summary>Maps the request's Verify URL to the identity of the user who controls it, or null if unrecognized.</summary>
    public IdentifyUserDelegate OnIdentifyUser { get; set; }
        = _ => throw new NotImplementedException($"{nameof(OnIdentifyUser)} not configured.");

    /// <summary>Downloads the verification hash text published at the Verify URL.</summary>
    public GetVerificationHashDelegate OnGetVerificationHash { get; set; }
        = _ => throw new NotImplementedException($"{nameof(OnGetVerificationHash)} not configured.");

    /// <summary>Optional diagnostic log hook, called at each stage of authentication. Does nothing by default.</summary>
    public LogWriteDelegate OnLogWrite { get; set; }
        = Helpers.DefaultLogWrite;

    public void SetSyncHostValidate(Func<string, bool> fn)
        => OnHostValidate = host => Task.FromResult(fn(host));

    public void SetSyncNowValidate(Func<long, bool> fn)
        => OnNowValidate = now => Task.FromResult(fn(now));

    public void SetSyncUnusValidate(Func<string, bool> fn)
        => OnUnusValidate = unus => Task.FromResult(fn(unus));

    public void SetSyncIdentifyUser(Func<Uri, string?> fn)
        => OnIdentifyUser = verify => Task.FromResult(fn(verify));

    public void SetSyncGetVerificationHash(Func<Uri, string> fn)
        => OnGetVerificationHash = verify => Task.FromResult(fn(verify));
}
