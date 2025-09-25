/* Demo Application for HashBackCore. */

/* Disable the async-has-no-await warning. This demo has a lot of them. I 
 * imagine your real-world app will want to connect to a database or similar. */
#pragma warning disable CS1998

using billpg.HashBackCore;
using System.Collections.Concurrent;

Console.WriteLine("Welcome to the HashBackCore Demo!");
Console.WriteLine();
Console.WriteLine("HashBackBuilder.");

/* Generate a verify URL. */
async Task<string> MyGenerateVerify()
{
    return $"https://client.example/hashback?id={Guid.NewGuid()}";
}

/* Register a verification hash. */
var registedHashes = new ConcurrentDictionary<string, string>();
async Task MyHashRegister(string verifyUrl, string hash)
{
    registedHashes[verifyUrl] = hash;
}

/* Build a HashBackBuilder object. */
var builder = new HashBackBuilder();
builder.Host = "server.example";
builder.VerifyGetter = MyGenerateVerify;
builder.HashRegister = MyHashRegister;

/* Generate a HashBack header. */
var authHeader = await builder.Build();
Console.WriteLine($"Authorization: HashBack {authHeader}");


