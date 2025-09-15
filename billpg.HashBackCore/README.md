# HashBackCore

**HashBackCore** is a reference implementation of the HashBack authentication protocol, written in C#. It provides core logic for building and parsing HashBack Authorization headers and verifying those payloads - without handling HTTP requests directly.

HashBackCore is designed to be lightweight, testable and protocol-faithful. It supports both client-side and server-side operations, making it suitable for a variety of applications.

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

## 📚 Usage
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

## 🦔 Philosophy
HashBackCore is built with simplicity and clarity in mind. It focuses on the core aspects of the HashBack protocol, avoiding unnecessary complexity. The library is designed to be easily testable, with a strong emphasis on unit tests to ensure each component functions as intended.

The implementation adheres closely to the HashBack specification, ensuring that it can be used reliably in real-world applications. The library concentrates on the essential tasks of building and parsing headers, calculating hashes, and verifying payloads, leaving the handling of receiving and sending HTTP requests and responses to the developer's discretion.

HashBack is a protocol inspired by the metaphor:
> "Hi Bob, I'm Alice."
> "Prove it."
> "You know my number, call me back."

The protocol is a round trip. The hash, protected by TLS in both directions, is the handshake.

Hashbert the brainy hedgehog says:
> 🦔 "HashBackCore is a spiky little library that gets the job done without fuss."

## 🦉 [billpg.com](https://billpg.com) 
