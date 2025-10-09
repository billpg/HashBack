/* Demo Application for HashBackCore.
 * 
 * "A good demo doesn't just show you 
 * the handshake. It lets you shake 
 * hands with the code." 🦔
 */

/* Disable the async-has-no-await warning.
 * (This demo has a lot of them.) */
#pragma warning disable CS1998

/* This demo only depends on the HashBackCore library,
 * NewtonSoft's JSON library and dot-net itself. */
using billpg.HashBackCore;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Text;

Console.WriteLine("Welcome to the HashBackCore Demo!");
Console.WriteLine();

/* Generate a verify URL. */
async Task<string> MyGenerateVerify()
{
    /* Your function would return a URL that will later
     * be available for the remote service to GET the 
     * verification hash. If you need to allocate an ID
     * first, it should make that request here first. */
    Console.WriteLine($"Called: {nameof(MyGenerateVerify)}()");
    return $"https://client.example/hashback?id={Guid.NewGuid()}";
}

/* Register a verification hash. */
var registeredHashes = new ConcurrentDictionary<string, string>();
async Task MyHashRegister(string verifyUrl, string hash)
{
    /* Your code should make the neccessary steps for 
     * the verification hash to finally be available 
     * to be returned when the remote server makes that
     * GET request. The URL parameter is the one your
     * URL generator returned esarlier. */
    Console.WriteLine(
        $"Called: {nameof(MyHashRegister)}(\r\n" +
        $"{new string(' ', 10)}\"{verifyUrl}\",\r\n" +
        $"{new string(' ', 10)}\"{hash}\")");
    registeredHashes[verifyUrl] = hash;
}

/* Build a HashBackBuilder object. */
var builder = new HashBackBuilder();
builder.Host = "server.example";
builder.VerifyGetter = MyGenerateVerify;
builder.HashRegister = MyHashRegister;

/* Generate a HashBack header. */
var authHeader = await builder.Build();

/* Write header to log in chunks. */
var fullHeader = new StringBuilder("Authorization: HashBack " + authHeader);
for (int i = 80; i < fullHeader.Length; i += 82)
    fullHeader.Insert(i, "\r\n    ");
Console.WriteLine(fullHeader);

/* Decode the base-64 into JSON. */
Console.WriteLine("Header as JSON...");
var authHeaderAsBytes = Convert.FromBase64String(authHeader);
var authHeaderAsJson = Encoding.UTF8.GetString(authHeaderAsBytes);
var authHeaderAsJObject = JObject.Parse(authHeaderAsJson);
Console.WriteLine(authHeaderAsJObject);

/* Send the request to the server with the generated base-64 block as the
 * Authorization header. This demo app will proceed in the role of that
 * server parsing and validating the request. */

/* Convert a verification URL into a user ID. */
async Task<string?> MyUserIdentifier(string verify)
{
    /* Your code should take the URL supplied and lookup who owns that
     * URL, returning that user's ID, or null if there no user who
     * owns that URL. */
    Console.WriteLine($"Called: {nameof(MyUserIdentifier)}(\"{verify}\")");
    return new Uri(verify).Host;
}

/* Download the verification hash from the supplied URL. */
async Task<string> MyHashGetter(string verify)
{
    /* Your code should use your favourite HTTP client library to 
     * download the verification hash from the supplied URL. If the
     * response is 200 and "text/plain", return the text as a string.
     * The caller will check the hash is as expected. */
    Console.WriteLine($"Called: {nameof(MyHashGetter)}(\"{verify}\")");
    return registeredHashes[verify];
}

/* Set up the validator object. */
var validator = new HashBackValidator();
validator.RequireHost("server.example");
validator.RequireNowWindow(10);
validator.OnIdentifyUser = MyUserIdentifier;
validator.OnGetHash = MyHashGetter;

/* Validate the Authorization header. */
var varifiedUser = await validator.Validate(authHeader);
Console.WriteLine($"Verified User: {varifiedUser}");
