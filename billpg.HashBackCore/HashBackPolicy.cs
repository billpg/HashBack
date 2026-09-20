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
    /// <summary>Checks whether the given Host value is acceptable. See <see cref="OnHostValidate"/>.</summary>
    public delegate Task<bool> HostValidateDelegate(string host);

    /// <summary>Checks whether the given Now value (Unix seconds) is acceptable. See <see cref="OnNowValidate"/>.</summary>
    public delegate Task<bool> NowValidateDelegate(long now);

    /// <summary>Checks whether the given Unus value is acceptable. See <see cref="OnUnusValidate"/>.</summary>
    public delegate Task<bool> UnusValidateDelegate(string unus);

    /// <summary>Maps a Verify URL to the identity of the user who controls it, or null if unrecognized. See <see cref="OnIdentifyUser"/>.</summary>
    public delegate Task<string?> IdentifyUserDelegate(Uri verify);

    /// <summary>Downloads the verification hash text published at a Verify URL. See <see cref="OnGetVerificationHash"/>.</summary>
    public delegate Task<string> GetVerificationHashDelegate(Uri verify);

    /// <summary>Receives a diagnostic log line. See <see cref="OnLogWrite"/>.</summary>
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

    /// <summary>Sets <see cref="OnHostValidate"/> from a simpler, non-async function.</summary>
    public void SetSyncHostValidate(Func<string, bool> fn)
        => OnHostValidate = host => Task.FromResult(fn(host));

    /// <summary>Sets <see cref="OnNowValidate"/> from a simpler, non-async function.</summary>
    public void SetSyncNowValidate(Func<long, bool> fn)
        => OnNowValidate = now => Task.FromResult(fn(now));

    /// <summary>Sets <see cref="OnUnusValidate"/> from a simpler, non-async function.</summary>
    public void SetSyncUnusValidate(Func<string, bool> fn)
        => OnUnusValidate = unus => Task.FromResult(fn(unus));

    /// <summary>Sets <see cref="OnIdentifyUser"/> from a simpler, non-async function.</summary>
    public void SetSyncIdentifyUser(Func<Uri, string?> fn)
        => OnIdentifyUser = verify => Task.FromResult(fn(verify));

    /// <summary>Sets <see cref="OnGetVerificationHash"/> from a simpler, non-async function.</summary>
    public void SetSyncGetVerificationHash(Func<Uri, string> fn)
        => OnGetVerificationHash = verify => Task.FromResult(fn(verify));
}
