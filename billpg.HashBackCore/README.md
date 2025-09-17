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
### `AuthHeaderBuilder``
The `AuthHeaderBuilder` class is used to create HashBack authorization headers. You set up the builder object (keep it long term or throw it away when you're done) and call the `Build` function to generate the final header and verification hash.

The builder class has a number of "`With`" functions that set various properties. These will return a new builder object with the property set, leaving the original unchanged. You may chain these calls together to set multiple properties. If you call the same `With` function multiple times, the last one will take precedence.

### `WithHost`
This function sets the name of the remote host in the authenticaton request. This must be set to a value the remote server expects in order to avoid "passing-along" attacks. This must be set once `Build()` is called, or an exception will be thrown. If your application always uses the same host name, you may prefer to set it once on the builder object and reuse that object for multiple requests.

```csharp
/* Create a builder object that will use this server name in requests. */
var builder = new AuthHeaderBuilder()
    .WithHost("server.example");
```

### `WithNow` and `WithNowGetter`
These functions set the source of the "Now" property of the authentication request. The default is to use `DateTime.UtcNow` to get the current time in seconds since the Unix epoch. You can use the applicable function to set a custom time source or provide a fixed value and you may use either a DateTime value or an integer in 1970-Epoch-Seconds. This is useful for testing or if you want to use a different time source, but I anticipate most users will not need to call this function, keeping the default.
```csharp
/* Create a builder object that will use a fixed time. */
var builder = new AuthHeaderBuilder()
    .WithNow(1700000000); // Fixed time for testing
```

### `WithUnus` and `WithUnusGetter`
These functions set the "Unus" property of the authentication request. The default is to generate a random 16-character string using a cryptographically secure random number generator. You can use the applicable function to set a custom Unus source or provide a fixed value. This is useful for testing or if you want to use a specific Unus value, but I anticipate most users will not need to call this function, keeping the default.
```csharp
/* Create a builder object that will use a fixed unus. */
var builder = new AuthHeaderBuilder()
    .WithUnus("fixedunusvalue"); // Fixed unus for testing
```

### `WithVerify` and `WithVerifyGetter`
These functions set the URL that the remote server will call to get the verification hash. This must be set once `Build()` is called, or an exception will be thrown. You may provide a fixed string or a function that returns a string. The latter is useful if your verification URLs always follow the same format with a small variation.
```csharp
/* Create a builder object that will use this verify URL in requests. */
var builder = new AuthHeaderBuilder()
    .WithVerify("https://client.example/api/hashback?id=502542886");

/* Or, create a builder object that will generate the verify URL each time. */
var random = new Random();
var builder = new AuthHeaderBuilder()
    .WithVerify(() => $"https://client.example/api/hashback?id={random.Next(999999999)});
```

## `WithPostBuild`
This function sets a callback that will be called after the header is built, but before the result is returned. This is useful if you want to log the result or store it somewhere. The callback receives an `AuthHeaderResult` object, which contains the `AuthHeader`, `VerifyUrl` and `VerificationHash` properties. (This mechanism is used with the `WithHashRegistry` function below.)
```csharp
/* Create a builder object that will log the result after building. */
var builder = new AuthHeaderBuilder()
    .WithPostBuild(result => 
    {
        Console.WriteLine("Built HashBack Auth Header: " + result.AuthHeader);
        Console.WriteLine("Verify URL: " + result.VerifyUrl);
        Console.WriteLine("Verification Hash: " + result.VerificationHash);
    });
```

### `WithHashRegistry`
This function sets up the builder object with the means to generate the verification URL and to supply a registration function that will deal with the verification hash. This is a convenience function that combines `WithVerify` and `WithPostBuild`. You provide the base URL and query string paarmeter name for the verification URL and a function that will be called to store the calculated verification hash against a Guid identifier.

This allows you to configure a long-lived builder object that can be reused for multiple requests which is already set up to deal with hash registration. Once configured, you can call `BuildAuthHeader()` function that returns only the authorization header string, leaving dealing with the details of the verification hash to the registration function.

There is a variant of this function that allows you to provide a `Func<Guid>` that generates the Guid identifier, if you want to use something other than a random Guid.

```csharp
/* Create a builder object that will use a hash registry. */
var builder = new billpg.HashBackCore.AuthHeaderBuilder()
    .WithHashRegistry(
        baseUrl: "https://example.com/hashback",
        queryParamName: "id",
        register: RegisterHash);

/* The above object will call this function
 * when it has generated a verification hash. */
void RegisterHash(Guid id, string hash)
{
    /* In a real application, you would store the hash in a 
     * database, upload it as a text file to your website's
     * SFTP folder, or otherwise save it for later retrieval.
     * This example only prints it to the console. */
    Console.WriteLine($"Register hash for id {id}: {hash}");
}

/* Now you can build an auth header, which will
 * also call the RegisterHash function above. */
request.AddHeader(
    "Authorization", 
    "HashBack " 
    + builder.WithHost("server.example").BuildAuthHeader());

/* The remote server receiving this Authorization request will 
 * attempt to load your verification hash by making a GET request
 * to "https://example.com/hashback?id=(guid)". The handler for
 * this GET request should load the guid from the query string
 * and return the hash string that was registered above. */
```
### `Build` and `BuildAuthHeader`
The `Build` function generates the HashBack authorization header and verification hash. The result is an `AuthHeaderResult` object, which contains three properties:
- `AuthHeader` - The string to send in the HTTP Authorization header.
- `VerifyUrl` - The URL that the remote server will call to get the verification hash.
- `VerificationHash` - The hash string that should be returned by that URL.

The `BuildAuthHeader` function is a convenience function that only returns the `AuthHeader` string. This is useful if you have already set up the builder to deal with the verification hash, perhaps using `WithHashRegistry`, and you only need the header string to send in the request.
```csharp
/* Create a builder object for a single use and log the result. */
var auth = new AuthHeaderBuilder()
    .WithHost("server.example")
    .WithVerify("https://client.example/api/hashback?id=502542886")
    .Build();
Console.WriteLine("Auth Header: " + auth.AuthHeader);
Console.WriteLine("Verification Hash: " + auth.VerificationHash);

/* Or, call this convenience function to 
 * avoid using a buider object at all. */
var auth = AuthHeaderBuilder.Build(
    "server.example",
    "https://client.example/api/hashback?id=502542886");
Console.WriteLine("Auth Header: " + auth.AuthHeader);
Console.WriteLine("Verification Hash: " + auth.VerificationHash);
```

> 🦔 "If you find yourself writing the same `With` calls over and over, consider creating a pre-configured builder object to reuse."

## 🦉 [billpg.com](https://billpg.com) 
