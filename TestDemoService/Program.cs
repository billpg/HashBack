using billpg.HashBackCore;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

Console.WriteLine("Hello, World!");

if (true)
{
    if (false)
    {
        using var http = new HttpClient();
        var resp = await http.GetAsync("http://localhost:9001/hello/");
        var content = await resp.Content.ReadAsStringAsync();
    }

    string verify = $"http://localhost:9001/hash/{Guid.NewGuid()}";

    string verificationHash = "";
    void RegisterHash(string x, string y)
    {
        verificationHash = y;
    }

    string ah = "";
    {
        var hbb = new HashBackBuilder();
        hbb.Host = "demo.hashback.dev";
        hbb.SetVerify(verify);
        hbb.SetSyncHashRegister(RegisterHash);
        ah = await hbb.Build();
    }



    {
        using var http = new HttpClient();

        // PUT a text/plain request to the hash service with a random GUID in the URL
        using var content = new StringContent(verificationHash, Encoding.ASCII, "text/plain");

        var response = await http.PutAsync(verify, content);

        Console.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response body: {responseBody}");
    }

    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("HashBack", ah);
        var resp = await http.GetAsync("http://localhost:9001/hello/");
        Console.WriteLine($"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        var content = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"Response body: {content}");

    }

}

if (true)
{

}