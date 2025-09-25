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
### `HashBackBuilder``
The `HashBackBuilder` class is used to create HashBack authorization headers. You set up the builder object (keep it long term or throw it away when you're done) and call the `Build` function to generate the final header and verification hash. 

The builder class has a number of properties that will control the content of the authentication header and how to capture the verification hash string.

The following properties should be set before calling `Build`:
- `Host` (Required)
  - A simple string value that will be used as the `Host` property.
  - The service you are attempting to authenticate to should publish exactly what string it expects here.
- `VerifyGetter` (Required)
  - An async function that will return a URL string for the `Verify` header. 
  - Set this to a function that will generate that URL, including allocating an ID if needed.
  - Helper functions allow you to set simple strings or sync functions.
- `HashRegister` (Optional)
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

See the example HashBackCoreDemo project for a complete demonstration.

### `AuthHeaderParser`
The `AuthHeaderParser` class is used to parse and validate HashBack authorization headers. Similar to the bulder object, you can set up a parser object the way you want and call the `Parse` function when you have a header. The parser object can be kept long term or discarded when you've finished with it. You may chain multiple "`With`" calls together to set various properties. If you call the same `With` function multiple times, the last one will take precedence.

#### `WithRequiredHost`
Sets the expected host name in the authentication request. Validation of the Host property is critical to prevent "passing-along" attacks. You service should document one string value that your service will support in client's requests (usually the full domain name of your web server) and call this function with that value to instruct the parser to expect only that value, rejecting any other value. 

```csharp
/* Create a parser object that will expect this server name in requests. */
var parser = new AuthHeaderParser()
    .WithRequiredHost("server.example");
```

#### `WithAnyRequiredHost`
Sets multiple acceptable host names in the authentication request. This is a variant of `WithRequiredHost` that allows you to specify more than one acceptable value. This may be useful if your service is known by multiple domain names or if you have multiple subdomains that all point to the same service.
```csharp
/* Create a parser object that will expect one of these server names in requests. */
var parser = new AuthHeaderParser()
    .WithAnyRequiredHost(new string[] 
    { 
        "server.example", 
        "www.server.example", 
        "api.server.example" 
    });
```

#### `WithHostTest`
Sets a custom test function to validate the Host property of the authentication request. This is a more flexible variant of `WithRequiredHost` that allows you to provide your own logic for validating the host name. The function receives the host string from the request and should return true if it is acceptable, or false otherwise. This may be useful if your validation logic is more complex than simply matching against a fixed string or list of strings, such as checking against a database or applying custom rules.

```csharp
/* Create a parser object that will use a custom host validation function. */
var parser = new AuthHeaderParser()
    .WithHostTest(host => host.EndsWith(".mydomain.example"));
```

#### `WithTimeTolerance`
Sets the allowed time drift in seconds when validating the Now property of the authentication request. By setting up the maximum tollerance, you are specifying exactly how much drift is allowed between the client and server clocks. If the request has a time too far from the current time, the request will be rejected.

A variant of this function allows you to provide a custom clock, if you want to use a different time source than `DateTime.UtcNow`. This may be useful for testing or if your server has a different time source. Variants allow your getter function to return either a `DateTime` or an integer in 1970-Epoch-Seconds.

```csharp
/* Create a parser object that will allow up to 10 seconds drift. */
var parser = new AuthHeaderParser()
    .WithTimeTolerance(10); // Allow 10 seconds drift.

/* Or, use a different clock other than DateTime.UtcNow. */
var parser = new AuthHeaderParser()
    .WithTimeTolerance(
        /* Server clock is 5 seconds fast. */
        () => DateTime.UtcNow.AddSeconds(5), 
        10);
```

#### `WithNowTest`
This function allows you to configure a parser object with a custom test for the value of a request's `Now` property. The function you supply will take a `DateTime` or `long` value and return `true` if that time is valid or `false` if it isn't.

```csharp
/* Create a parser that will use the supplied custom Now validator. */
var parser = new AuthHeaderParser()
    /* Only timestamps ending in 12 are valid. */
    .WithNowTest(now => now % 100 == 12);
```

#### `Parse`
This function will parse and validate an authorization header block, returning the pertinent details. If the header block fails basic validation, the function will throw a custom exception detailing the reasons for rejecting that block.

If not an exception, the function will return an object with these properties:
- `VerifyUrl` - The URL that identifies the user and where to retrieve the verification hash.
- `ExpectedHash` - The hash string expected from this URL.

This function **neither** checks if the verification URL maps to a known user, nor does it retrieve the verification hash itself. That task is left to the caller. A request must not be considered valid until that verification has taken place.

> 🦔 "If you find yourself writing the same `With` calls over and over, consider creating a pre-configured builder object to reuse."

## 🦉 [billpg.com](https://billpg.com) 
