# HashBackCore

**HashBackCore** is a C# reference implementation of the HashBack authentication protocol, public draft version 4.1. It provides core logic for building and parsing HashBack Authorization headers and verifying those payloads - without handling HTTP requests directly. The only dependencies are dot-net itself and NewtonSoft's JSON library.

HashBackCore is designed to be lightweight, testable and protocol-faithful. It supports both client-side and server-side operations, making it suitable for a variety of applications. The library is focused on the essential tasks of header construction and parsing. To make it flexible and unit-testable, almost all of the external interfaces are in the form of `Func` and `Action` delegates. If you don't like `DateTime.UtcNow`, you can use your own.

> 🦔 "I'm Hashbert, the brainy hedgehog, here to help you with HashBack. I may be small, but I've got a big brain for hashing things out!"

> 🦔 "I never said I was good at puns."

## 🚀 Features

- 🔒 **Header Building** - Create valid HashBack authorization headers.
- 🔍 **Header Parsing** - Extract and validate components from HashBack authorization headers.
- 🧮 **Hash Calculation** - Compute correctly salted verification hashes using SHA-256.
- ✅ **Payload Verification** - Verify payloads against provided hashes.
- 🧪 **Unit Tested** - Comprehensive unit tests to ensure reliability and correctness.

## 📦 Installation
HashBackCore is not yet published to NuGet. To use it in your project, clone the repository and include the project in your solution.
```bash
git clone https://github.com/billpg/HashBack
cd HashBack/HashBackCore
```
Then, add a reference to HashBackCore in your project file (.csproj):

> 🦔 "If you're wondering why it's not on NuGet yet, it's because trust can't be packaged. Also, Bill hasn't signed up yet. Stupid owl."

## 📚 Quick Start
### Client-Side Example
```csharp
/* Build a fresh request for a named remote host, saying where
 * that server can fetch our verification hash from. */
var request = billpg.HashBackCore.HashBackRequest.Create(
    "server.example",
    new Uri("https://client.example/api/hashback?id=502542886"));

/* Log the result. */
Console.WriteLine("Auth Header: " + request.AuthToken);
Console.WriteLine("Verification Hash: " + request.VerificationHash);

/* Now send "HashBack " + request.AuthToken in an HTTP Authorization
 * header. When (if) that server fetches the above verification
 * URL, have that respond with request.VerificationHash. */
```

### Server-Side Example
```csharp
/* Construct a policy object that expects the supplied host name
 * and allows up to ten seconds drift in the time-stamp. */
var policy = new billpg.HashBackCore.HashBackPolicy();
policy.RequireHost("server.example");
policy.RequireNowWindow(10);
policy.SetSyncIdentifyUser(verify => LookUpUserForVerifyUrl(verify));
policy.SetSyncGetVerificationHash(verify => DownloadVerificationHash(verify));

/* Parse the header received in an Authorization header and
 * authenticate it against the policy in one call. */
string user = await billpg.HashBackCore.HashBackRequest.Authenticate(
    "eyHeaderTokenGoesHere==", policy);
Console.WriteLine("Authenticated as: " + user);
```

> 🦔 "Remember, every copy/pasted line is a promise you'll try to understand it later."

## 🧠 Philosophy
HashBackCore is built with simplicity and clarity in mind. It focuses on the core aspects of the HashBack protocol, avoiding unnecessary complexity. The library is designed to be easily testable, with a strong emphasis on unit tests to ensure each component functions as intended.

The implementation adheres closely to the HashBack specification, ensuring that it can be used reliably in real-world applications. The library concentrates on the essential tasks of building and parsing headers, calculating hashes, and verifying payloads, leaving the handling of receiving and sending HTTP requests and responses to the developer's discretion.

> 🦔 "HashBackCore is a spiky little library that gets the job done without fuss."

## 🛠️ Usage

### `HashBackRequest`
`HashBackRequest` represents a single HashBack request, whether you're building one to send or parsing one you've received. It's used on both the client and server side.

On the client side, call the static `Create` method to build a fresh request:
- `HashBackRequest.Create(host, verify)`
  - Builds a request for right now, with a fresh cryptographic-quality random `Unus` value.
- `HashBackRequest.Create(host, now, verify)` / `HashBackRequest.Create(host, now, unus, verify)`
  - Overloads that let you supply your own `Now` and/or `Unus` values, useful for testing.

Once built, read these properties to send the request and publish its hash:
- `AuthToken` - the BASE-64 encoded JSON block, ready to append to an `Authorization: HashBack ` header.
- `VerificationHash` - the hash to publish at the `Verify` URL.

On the server side, call the static `Parse` method with the incoming header value:
- `HashBackRequest.Parse(authHeader)`
  - Checks the header is well-formed - including that the `Verify` URL is secure - and returns a `HashBackRequest` with the `Host`, `Now`, `Unus` and `Verify` properties extracted. It does not yet apply any of your own server policy; call `Authenticate` for that (see below).
  - Throws `AuthorizationParseException` if the header is malformed.

### `HashBackPolicy`
On the server side, the `HashBackPolicy` class holds the pluggable rules your server applies once a header has been parsed - is the `Host` one of ours, is `Now` recent enough, has this `Unus` been used before, who does the `Verify` URL belong to, and what's published there. Configure the delegate properties directly, or use the extension methods below for the common cases.

- `.RequireHost(host)` or `.RequireAnyHost(host1, host2)`
  - Configures the policy to only accept a supplied string as a `Host` value.
  - Use your website's full domain name.
  - Validation of the `Host` property is critical to prevent "passing-along" attacks. Your service should document one string value that your service will support in clients' requests (usually the full domain name of your web server) and call this function with that value.
  - If you have many acceptable `Host` strings, the "Any" variant will accept a collection of strings.
- `.RequireNowWindow(seconds)`
  - Configures the policy to only allow up to this many seconds variance from the system clock in the client's `Now` value.
  - Variants allow for a different number of seconds in the past or future, or to use a different clock to the system one.
- `.RequireUnusNotReused(window)`
  - Configures the policy to reject any `Unus` value already seen within the supplied `TimeSpan`, keeping an in-memory record of previously seen values.
  - Pick a window at least as wide as your `Now` tolerance, since an older request could never pass the `Now` check anyway.
  - Without this, the default accepts any well-formed `Unus` value - the format itself is already checked while parsing the header.
- `OnIdentifyUser`
  - Set this property to an async function that will convert a `Verify` URL into the user that owns that URL, or null if the URL doesn't belong to any user your system knows about. This check ensures that authentication only proceeds if the URL belongs to an identifiable user.
  - The string returned by your handler will be the same string returned by `Authenticate` if all tests pass.
  - Other than null/not-null, HashBackCore doesn't apply any meaning to the string's value and will blindly pass it along.
  - `SetSyncIdentifyUser(fn)` sets this from a simpler non-async handler.
- `OnGetVerificationHash`
  - Set this property to an async handler that will download the verification hash from the supplied URL.
  - Any exceptions thrown, such as because of network failures, will fall to the caller.
  - The return value should be the text returned from that URL.
  - `SetSyncGetVerificationHash(fn)` sets this from a simpler non-async handler.
  - HashBackCore deliberately has no default implementation of this - it doesn't interface with HTTP itself, leaving that to your own code (or the ASP.NET Core authenticator package, which does provide one).

Once configured, call `request.Authenticate(policy)` (or the static shortcut `HashBackRequest.Authenticate(authHeader, policy)` to parse and authenticate in one step). This checks `Host`, `Now` and `Unus`, identifies the user from `Verify`, downloads the verification hash, and confirms it matches. It returns the identified user string on success.

If authentication is rejected for whatever reason, the function will throw an `AuthorizationParseException` error, with these properties:
- `Message` (inherited from the `Exception` base class.)
    - Describes (in English) the reason for rejecting the request.
- `Reason`
    - An enum value that identifies the specific problem. Allowed values are:
      - `BadHeader` - The header itself is malformed.
      - `WrongHost` - The `Host` value is not on the allowed list.
      - `WrongNow` - The `Now` value is outside the allowed window.
      - `ReplayedUnus` - The `Unus` value has already been used, per `RequireUnusNotReused`.
      - `UnknownUser` - The `Verify` value doesn't correspond to a known user.
      - `WrongHash` - The downloaded verification hash did not match the expected hash.

## In closing...

> 🦔 "We hope you find this useful. If you have feedback, please raise a ticket on out github."

## 🦉 [billpg.com](https://billpg.com) 
