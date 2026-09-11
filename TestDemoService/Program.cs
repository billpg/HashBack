using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

Console.WriteLine("Hello, World!");

/* Build a JSON claim. */
var claimAsJson = new JsonObject();
claimAsJson["Version"] = "BILLPG_DRAFT_4.2";
claimAsJson["Host"] = "demo.hashback.dev";
claimAsJson["Now"] = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
claimAsJson["Unus"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
string verifyUrl = $"https://billpg.com/hash/1da34f7e-2c46-4a2b-8d05-6ad50db77ed5.txt";
claimAsJson["Verify"] = verifyUrl;

/* Hash the claim. */
byte[] claimAsBytes = Encoding.ASCII.GetBytes(claimAsJson.ToString());
byte[] salt = Convert.FromBase64String("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=");
byte[] hashInput = Enumerable.Concat(salt, claimAsBytes).ToArray();
byte[] hashAsBytes = SHA256.HashData(hashInput);
string hashAsText = Convert.ToBase64String(hashAsBytes);
Console.WriteLine(verifyUrl);;
Console.WriteLine(hashAsText);
Console.ReadLine();

#if false
/* Upload the hash. Replace this with uploading to your own website. */
HttpClient httpPut = new HttpClient();
var respPut = await httpPut.PutAsync(verifyUrl, new StringContent(hashAsText));
respPut.EnsureSuccessStatusCode();
#endif

/* Call the hello end-point. */
HttpClient httpHello = new HttpClient();
httpHello.DefaultRequestHeaders.Add("Authorization", "HashBack " + Convert.ToBase64String(claimAsBytes));
var respHello = await httpHello.GetAsync("http://demo.hashback.dev/hello/");
respHello.EnsureSuccessStatusCode();
var helloBody = await respHello.Content.ReadAsStringAsync();

/* Report results. */
Console.WriteLine(respHello.StatusCode);
foreach (var header in respHello.Headers)
    Console.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
Console.WriteLine(helloBody);

