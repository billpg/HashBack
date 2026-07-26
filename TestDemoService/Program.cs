using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

Console.WriteLine("Hello, World!");

using var http = new HttpClient();

// PUT a text/plain request to the hash service with a random GUID in the URL
var url = $"http://localhost:9001/hash/{Guid.NewGuid()}";
using var content = new StringContent("xyz", Encoding.UTF8, "text/plain");

var response = await http.PutAsync(url, content);

Console.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
var responseBody = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Response body: {responseBody}");