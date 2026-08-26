using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

Console.WriteLine("Hello, World!");

/* Build a JSON claim. */
var claimAsJson = new JsonObject();
claimAsJson["Version"] = "BILLPG_DRAFT_4.2";
claimAsJson["Host"] = "localhost:9001";
claimAsJson["Now"] = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
claimAsJson["Unus"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
string verifyUrl = $"http://localhost:9001/hash/{Guid.NewGuid()}";
claimAsJson["Verify"] = verifyUrl;

/* Hash the claim. */
byte[] claimAsBytes = Encoding.ASCII.GetBytes(claimAsJson.ToString());
byte[] salt = Convert.FromBase64String("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=");
byte[] hashInput = Enumerable.Concat(salt, claimAsBytes).ToArray();
byte[] hashAsBytes = SHA256.HashData(hashInput);
string hashAsText = Convert.ToBase64String(hashAsBytes);

/* Upload the hash. Replace this with uploading to your own website. */
HttpClient httpPut = new HttpClient();
var respPut = await httpPut.PutAsync(verifyUrl, new StringContent(hashAsText));
respPut.EnsureSuccessStatusCode();

/* Call the hello end-point. */
HttpClient httpHello = new HttpClient();
httpHello.DefaultRequestHeaders.Add("Authorization", "HashBack " + Convert.ToBase64String(claimAsBytes));
var respHello = await httpHello.GetAsync("http://localhost:9001/hello/");
respHello.EnsureSuccessStatusCode();
var helloBody = await respHello.Content.ReadAsStringAsync();

{ }
