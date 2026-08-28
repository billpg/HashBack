# billpg.HashBackAspNetCore

An ASP.NET Core authentication handler for the [HashBack](../README.md) protocol. Drop this into a web service project to accept `Authorization: HashBack ...` headers using the standard ASP.NET Core authentication pipeline - `[Authorize]`, `HttpContext.User`, `WWW-Authenticate` challenges and all.

This package is a thin ASP.NET Core adapter around [`billpg.HashBackCore`](../billpg.HashBackCore/README.md), which does the actual protocol work (parsing, hashing, policy checks).

## 📦 Installation
Not yet published to NuGet. Clone this repository and add a project reference to `billpg.HashBackAspNetCore.csproj`.

## 🚀 Registering the scheme
In `Program.cs`:
```csharp
builder.Services.AddAuthentication(HashBackAuthenticationDefaults.AuthenticationScheme)
    .AddHashBack(options =>
    {
        /* Required: which Host value(s) this server accepts. */
        options.Policy.RequireHost("myservice.example");

        /* Required: map a Verify URL to the identity of the caller who owns it. */
        options.Policy.SetSyncIdentifyUser(verify => LookUpUserForVerifyUrl(verify));

        /* Recommended: replay protection, and how much clock drift to tolerate. */
        options.Policy.RequireNowWindow(30);
        options.Policy.RequireUnusNotReused(TimeSpan.FromMinutes(5));

        /* Optional: shown in the WWW-Authenticate challenge header. */
        options.Realm = "myservice.example";
    });

builder.Services.AddAuthorization();
// ...
app.UseAuthentication();
app.UseAuthorization();
```

Then protect an endpoint the normal ASP.NET Core way:
```csharp
app.MapGet("/hello", (ClaimsPrincipal user) => $"Hello {user.FindFirst(ClaimTypes.NameIdentifier)!.Value}!")
    .RequireAuthorization();
```

## 🔍 What it does for you
- Parses and validates the incoming `Authorization` header using `HashBackRequest`.
- Runs your configured `HashBackPolicy` (host, time window, replay, user identification).
- Downloads the verification hash itself, using a built-in `HttpClient`-based fetcher (no redirects followed, short timeout) - unless you set `options.UseDefaultVerificationHashFetcher = false` and assign `options.Policy.OnGetVerificationHash` yourself, which is recommended if you need stricter protections such as SSRF-safe IP filtering (see `DemoService`'s own `HttpGetter` for an example of that level of hardening).
- On success, builds a `ClaimsPrincipal` carrying the identified user as a `ClaimTypes.NameIdentifier` claim.
- On failure, issues a `WWW-Authenticate: HashBack` challenge header (with an optional `realm`) alongside the `401` response, per the HashBack specification.

## ⚠️ What it deliberately doesn't do
- It doesn't cache successful authentications behind a cookie or bearer token. The HashBack exchange is relatively expensive, so most services will want to issue a short-lived cookie or token after the first successful authentication and accept that on subsequent requests instead - that's application-specific and left to you. (`DemoService`'s `HelloController` shows one way to do this.)
