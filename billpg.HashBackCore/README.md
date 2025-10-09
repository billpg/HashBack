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
/* Construct a builder object. */
var hashbackBuilder = new billpg.HashBackCore.AuthHeaderBuilder();

/* Build an authorization header for a named remote 
 * host and where it can fetch our verification hash. */
var auth = hashbackBuilder
    .WithHost("server.example")
    .WithVerify("https://client.example/api/hashback?id=502542886")
    .Build();

/* Log the result of the builder. */
Console.WriteLine("Auth Header: " + auth.AuthHeader);
Console.WriteLine("Verification Hash: " + auth.VerificationHash);

/* Now send the auth.AuthHeader value in an HTTP Authorization
 * header. When (if) that server fetches the above verification
 * URL, have that respond with the verification hash. */
```

### Server-Side Example
```csharp
/* Construct an parser object that expects the supplied host name
 * and allows up to ten seconds drift in the time-stamp. */
var hashbackParser = new billpg.HashBackCore.AuthHeaderParser()
    .WithRequiredHost("server.example")
    .WithTimeTolerance(10);

/* Parse the header received in an Authorization header. */
var parseResult = hashbackParser.Parse("eyHeaderTokenGoesHere==");
Console.WriteLine("Verify URL: " + parseResult.VerifyUrl);
Console.WriteLine("Expected Hash: " + parseResult.ExpectedHash);

/* Now fetch the Verify URL, where we expect 
 * to find the same string as the expected hash. */
```

> 🦔 "Remember, every copy/pasted line is a promise you'll try to understand it later."

## 🧠 Philosophy
HashBackCore is built with simplicity and clarity in mind. It focuses on the core aspects of the HashBack protocol, avoiding unnecessary complexity. The library is designed to be easily testable, with a strong emphasis on unit tests to ensure each component functions as intended.

The implementation adheres closely to the HashBack specification, ensuring that it can be used reliably in real-world applications. The library concentrates on the essential tasks of building and parsing headers, calculating hashes, and verifying payloads, leaving the handling of receiving and sending HTTP requests and responses to the developer's discretion.

> 🦔 "HashBackCore is a spiky little library that gets the job done without fuss."

## 🛠️ Usage

See the demo console app HashBackCore for runnable sample code in a copy-paste-friendly form.

### `HashBackBuilder`
On the client side, the `HashBackBuilder` class is used to create HashBack authorization headers. You set up the builder object (keep it long term or throw it away when you're done) and call the `Build` function to generate the final header and verification hash. 

The builder class has a number of properties that will control the content of the authentication header and how to capture the verification hash string.

The following properties should all be set before calling `Build`:
- `Host`
  - A simple string value that will be used as the `Host` property.
  - The service you are attempting to authenticate to should publish exactly what string it expects here.
- `VerifyGetter`
  - An async function that will return a URL string for the `Verify` header. 
  - Set this to a function that will generate that URL, including allocating an ID if needed.
  - Helper functions allow you to set simple strings or sync functions.
- `HashRegister`
  - An aync function that will be called to register an expected hash with the verify URL generated earlier.
  - Set this to a function that will store the verification hash for retrieval later, perhaps in a database or a file on a web service.
  - The default handler saves hashes to the builder object where they can be retrieved later.

These optional helpers provide pre-packaged handlers for these properties:
- `SetSyncVerifyGetter(fn)`
    - Sets the `VerifyGetter` with a simpler non-async handler.
- `SetVerify(url)`
    - If you don't want to supply a getter function, this function allows you to set a simple string.
    - Useful if your builder object is intended for a single use and you already have the URL you want to use.
- `SetSyncHashRegister(fn)`
    - Sets a simple non-async handler to be called when a verification hash is ready.
    - Useful if your verification hash storage is a simple in-memory collection.

These properties are for advanced uses only. Normal uses should leave the defaults in place..
- `NowGetter`
  - A function that will get the value to use as the `Now` property.
  - The default handler uses `DateTime.UtcNow`.
- `UnusGetter`
  - A function that will get the value to use as the `Usus` proprty.
  - The default handler uses a cryptographic quality ransom byte source.

The `Build` function calls these various handlers to build the JSON request. On the way, it calls the `HashRegister` handler with the retrieved verification URL and the calculated verification hash. The function returns the encoded header value suitable for embedding into the HTTP `Authorization` header.

### `HashBackValidator`
On the server side, the `HashBackValidator` class is used to parse and validate HashBack authorization headers. Similar to the builder object, you can set up a parser object the way you want and call the `Validate` function when you have a header.

To configure a validator object, use the following:
- `.RequireHost(host)` or `.RequireAnyHost(host1, host2)`
  - Configures the validator to only accept a supplied string as a `Host` value.
  - Use your website's full domain name.
  - Validation of the Host property is critical to prevent "passing-along" attacks. You service should document one string value that your service will support in client's requests (usually the full domain name of your web server) and call this function with that value.
  - If you have many acceptable `Host` strings, the "Any" varient will accept a collection of strings.
- `.RequireNowWindow(seconds)`
  - Configures the validator to only allow up to this many seconds variace from the system clock in the client's `Now` value.
  - Variants allow for a different number of seconds in the past or future, or to use a different clock to the the system one.
- `OnIdentifyUser`
  - Set this property to an async function that will convert a verification URL into the user that owns that URL, or null if the URL doesn't belong to any user your system knows about. This check ensures that verification only proceeds if the URL belongs to an identifiable user.
  - The string returned by your handler will be the same string returned by the `Validate` functon if all tests pass.
  - Other than null/not-null, HashBackCore doesn't apply any meaning to the string's value and will blindly pass it along.
- `OnGetHash`
  - Set this property to an async handler that will download the verification hash from the supplied URL.
  - Any exceptions thrown, such as because of network failures, will fall to the caller.
  - The return value should be text returned from that URL. 

If validation is rejected for whatever reason, the function with throw a `AuthorizationParseException` error, with these properties:
- `Message` (inherited from the `Exception` base class.)
    - Describes (in English) the reason for rejecting the rquest.
- `Reason`
    - An enum value that identifies the specific problem. Allowed values are:
      - `BadHeader` - The header itself is malformed.
      - `WrongHost` - The `Host` value is not on the allowed list.
      - `WrongNow` - The `Now` value is outsode the allowed window.
      - `UnknownUser` - The `Verify` value doesn't correspond to a known user.
      - `WrongHash` - The downloaded verification hash did not match the expected hash.

## In closing...

> 🦔 "We hope you find this useful. If you have feedback, please raise a ticket on out github."

## 🦉 [billpg.com](https://billpg.com) 
