using Markdig.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AuthenticationHeaderValue = System.Net.Http.Headers.AuthenticationHeaderValue;

namespace DemoServiceTests;

[TestClass]
public class ServiceTests
{
    private static Process? serviceProc = null;
    private const string ServiceBaseUrl = "http://localhost:9001/";

    [ClassInitialize]
    public static void LaunchService(TestContext testContext)
    {
        /* Kill any service processes left behind. */
        foreach (var proc in Process.GetProcessesByName("DemoService"))
        {
            try { proc.Kill(); }
            catch { /* ignore */ }
        }

        /* Find the DemoService EXE and launch it. */
        var dllPath = typeof(DemoService.Helpers).Assembly.Location;
        var exePath = Path.ChangeExtension(dllPath, ".exe");
        var psi = new ProcessStartInfo(exePath)
        {
            Arguments = "GetLocalhost",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
        };
        serviceProc = Process.Start(psi);
        if (serviceProc == null)
            Assert.Fail($"Failed to start service process '{exePath}'.");

        bool isRunning = Task.Run(WaitForNowListening).Wait(5000);
        void WaitForNowListening()
        {
            while (true)
            {
                var line = serviceProc!.StandardOutput.ReadLine();
                if (line!.Contains("Now listening"))
                    return;
            }
        }
        if (!isRunning)
            Assert.Fail("Could not launch DemoService.");
    }

    [ClassCleanup]
    public static void StopService()
    {
        if (serviceProc != null)
        {
            try
            {
                if (!serviceProc.HasExited)
                    serviceProc.Kill();
            }
            catch { /* ignore */ }
        }
    }

    private JObject BuildClaim(string? verify = null)
        => new JObject
        {
            ["Version"] = "BILLPG_DRAFT_4.2",
            ["Host"] = new Uri(ServiceBaseUrl).Authority,
            ["Now"] = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds,
            ["Unus"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            ["Verify"] = verify ?? $"{ServiceBaseUrl}hash/{Guid.NewGuid()}"
        };

    private static (string hashAsText, string claimAsBase64) HashClaim(JObject claim)
    {
        byte[] claimAsBytes = Encoding.ASCII.GetBytes(claim.ToString());
        byte[] salt = Convert.FromBase64String("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=");
        byte[] hashInput = Enumerable.Concat(salt, claimAsBytes).ToArray();
        byte[] hashAsBytes = SHA256.HashData(hashInput);
        string hashAsText = Convert.ToBase64String(hashAsBytes);
        return (hashAsText, Convert.ToBase64String(claimAsBytes));
    }

    [TestMethod]
    public async Task Hello_HappyPath()
    {
        /* Build a JSON claim. */
        var claimAsJson = BuildClaim();

        /* Hash the claim. */
        (string hashAsText, string claimAsBase64) = HashClaim(claimAsJson);

        /* Upload the hash. */
        using (var httpPut = new HttpClient())
        {
            string verifyUrl = claimAsJson["Verify"]!.Value<string>()!;
            var respPut = await httpPut.PutAsync(verifyUrl, new StringContent(hashAsText));
            respPut.EnsureSuccessStatusCode();
        }

        /* Call the hello end-point. */
        var respHello = await CallHelloWithClaim(claimAsBase64);
        respHello.EnsureSuccessStatusCode();
        var helloBody = await respHello.Content.ReadAsStringAsync().ConfigureAwait(false);

        /* Check the response. */
        Assert.AreEqual("Hello localhost!", helloBody);
    }

    private async Task<HttpResponseMessage> CallHelloWithClaim(JObject claimAsJson)
    {
        var jsonAsBytes = Encoding.ASCII.GetBytes(claimAsJson.ToString());
        var base64 = Convert.ToBase64String(jsonAsBytes);
        return await CallHelloWithClaim(base64);
    }

    private async Task<HttpResponseMessage> CallHelloWithClaim(string claimAsBase64)
    {
        using HttpClient httpHello = new HttpClient();
        httpHello.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("HashBack", claimAsBase64);
        return await httpHello.GetAsync($"{ServiceBaseUrl}hello/").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Hello_NoAuth()
    {
        using HttpClient httpHello = new HttpClient();
        var respHello = await httpHello.GetAsync($"{ServiceBaseUrl}hello/").ConfigureAwait(false);
        Assert.AreEqual((HttpStatusCode)401, respHello.StatusCode);
        Assert.IsTrue(respHello.Headers.Contains("WWW-Authenticate"), "Response is missing WWW-Authenticate.");
        var authHeaders = respHello.Headers.GetValues("WWW-Authenticate").ToList();
        Assert.AreEqual(1, authHeaders.Count);

        // Consider loosening exact-string match if header formatting varies across environments
        Assert.AreEqual(
            "HashBack realm=\"demo.hashback.dev\" set-cookie=\"HashBackDemoService\"" +
            " version=\"BILLPG_DRAFT_4.2,BILLPG_DRAFT_4.1\"", authHeaders.Single());

        var helloBody = await respHello.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.IsTrue(helloBody.Contains("<html"));
    }

    [TestMethod]
    public async Task Hello_BadAuthorizationJson()
    {
        using HttpClient httpHello = new HttpClient();
        httpHello.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("HashBack", "ThisIsNotBase64EncodedJson==");
        var respHello = await httpHello.GetAsync($"{ServiceBaseUrl}hello/").ConfigureAwait(false);
        Assert.AreEqual((HttpStatusCode)400, respHello.StatusCode);
        Assert.AreEqual("application/problem+json", respHello.Content.Headers.ContentType?.MediaType);
        var errorJson = await respHello.Content.ReadAsStringAsync().ConfigureAwait(false);
        var error = JsonSerializer.Deserialize<ProblemDetails>(errorJson);
        Assert.IsNotNull(error);
        Assert.AreEqual(400, error.Status);
        Assert.AreEqual("Invalid Authorization Header", error.Title);
        Assert.AreEqual("Authorization header is not valid JSON.", error.Detail);
    }

    [TestMethod]
    public async Task Hello_BadVersion()
        => await Shared_Hello_BadJson("Version", "RUTABAGA",
            "Version must be one of 'BILLPG_DRAFT_4.1'/'BILLPG_DRAFT_4.2'.");

    [TestMethod]
    public async Task Hello_WrongHost()
        => await Shared_Hello_BadJson("Host", "rutabaga.example",
            "Host property is not valid for this server.");

    [TestMethod]
    public async Task Hello_FarPast()
        => await Shared_Hello_BadJson("Now", (long)1E9,
            "Now property is not valid for this server's time policy.");

    [TestMethod]
    public async Task Hello_ShortUnus()
        => await Shared_Hello_BadJson("Unus", "Rutabaga",
            "Unus property is not 128 bits.");

    [TestMethod]
    public async Task Hello_UnusIsNotBase64()
        => await Shared_Hello_BadJson("Unus", "I'm not base 64!",
            "Unus property is not base-64.");

    [TestMethod]
    public async Task Hello_VerifyIsNotAUrl()
        => await Shared_Hello_BadJson("Verify", "I'm not a URL.",
            "Verify property must be a valid URL.");

    [TestMethod]
    public async Task Hello_VerifyIsNotSecure()
        => await Shared_Hello_BadJson("Verify", "http://example.com/rutabaga",
            "Verify URL must be HTTPS.");

    public async Task Shared_Hello_BadJson<T>(string modJsonProp, T modValue, string expectedDetail)
    {
        /* Build a JSON claim but break it as directed by caller.
         * (We won't need to hash it because it shouldn't get that far.) */
        var claimAsJson = BuildClaim();
        claimAsJson[modJsonProp] = JToken.FromObject(modValue!);

        /* Call the API with this bad JSON. */
        var respHello = await CallHelloWithClaim(claimAsJson);

        /* Check the response. */
        await Assert_ErrorResponse(respHello, 400, "Invalid Authorization Header", expectedDetail);
    }

    private async Task Assert_ErrorResponse(
        HttpResponseMessage respHello, int expectedStatusCode, string expectedTitle, string expectedDetail)
    {
        Assert.IsNotNull(respHello);
        Assert.AreEqual((HttpStatusCode)400, respHello.StatusCode);
        Assert.IsNotNull(respHello.Content.Headers.ContentType);
        Assert.AreEqual("application/problem+json", respHello.Content.Headers.ContentType?.MediaType);
        var errorJson = await respHello.Content.ReadAsStringAsync().ConfigureAwait(false);
        var error = JsonSerializer.Deserialize<ProblemDetails>(errorJson);
        Assert.IsNotNull(error);
        Assert.AreEqual(expectedStatusCode, error.Status);
        Assert.AreEqual(expectedTitle, error.Title);
        Assert.AreEqual(expectedDetail, error.Detail);
    }

    [TestMethod]
    public async Task Hello_GoodHashRequest()
    {
        /* Open a web listener to return the verification hash. */
        using var osl = new OneShotHttpListen();
        osl.Start();

        /* Build a JSON claim. */
        var verifyUrl = new Uri($"http://localhost:{osl.ListenPort}/{Guid.NewGuid()}");
        var claimAsJson = BuildClaim(verify: verifyUrl.ToString());
        (var hash, var auth) = HashClaim(claimAsJson);

        /* Set up the listener to respond with the verification hash. */
        osl.RespondBody = hash;
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("HashBack", auth);
        var resp = await http.GetAsync("http://localhost:9001/hello/");

        /* Check the hello end-point's response. */
        Assert.AreEqual((HttpStatusCode)200, resp.StatusCode);
        Assert.AreEqual("text/plain", resp.Content.Headers.ContentType!.MediaType);
        Assert.AreEqual("Hello localhost!", await resp.Content.ReadAsStringAsync());

        /* Check the listener was used. */
        Assert.IsTrue(osl.Called);
        Assert.AreEqual(verifyUrl, osl.ReqUrl);
        Assert.AreEqual("demo.hashback.dev", osl.ReqHeaders!["User-Agent"]);
    }

    [TestMethod]
    public async Task Hello_VerificationOffline()
        => await SharedHello_BadVerificationUrl(
            "http://localhost:8001/xyz",
            "External URL not available.",
            "Can't connect to http://localhost:8001/xyz (127.0.0.1)");

    [TestMethod]
    public async Task Hello_VerificationOnIp()
        => await SharedHello_BadVerificationUrl(
            "https://192.0.2.1/xyz",
            "URL not acceptable.",
            "Host must be for a domain.");

    private async Task SharedHello_BadVerificationUrl(
        string arrangeVerifyUrl, string expectedTitle, string expectedDetail)
    {
        /* Build a JSON claim with a port that will never connect. */
        var verifyUrl = new Uri(arrangeVerifyUrl);
        var claimAsJson = BuildClaim(verify: verifyUrl.ToString());
        (_, var auth) = HashClaim(claimAsJson);

        /* Make the request with the above claim. */
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("HashBack", auth);
        var resp = await http.GetAsync("http://localhost:9001/hello/");

        /* Check the hello end-point's response. */
        await Assert_ErrorResponse(resp, 400, expectedTitle, expectedDetail);
    }

    [TestMethod]
    public async Task Hello_VerificationHash404()
    {
        /* Open a web listener to respond with a 404. */
        using var osl = new OneShotHttpListen();
        osl.RespondStatusCode = 404;
        osl.RespondBody = "No!";
        osl.Start();

        /* Build a JSON claim refering to that listener. */
        var verifyUrl = new Uri($"http://localhost:{osl.ListenPort}/xyz");
        var claimAsJson = BuildClaim(verify: verifyUrl.ToString());

        /* Call the API. */
        var respHello = await CallHelloWithClaim(claimAsJson);

        /* Assert the response. */
        await Assert_ErrorResponse(respHello, 400, "Bad Verification URL.",
            $"{verifyUrl} returned status code 404");
    }

    // --- New black-box tests for /hash endpoints ---

    [TestMethod]
    public async Task Hash_GetRoot_ReturnsHtml()
    {
        using var http = new HttpClient();
        var resp = await http.GetAsync($"{ServiceBaseUrl}hash/").ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.IsNotNull(resp.Content.Headers.ContentType);
        Assert.IsTrue(resp.Content.Headers.ContentType!.MediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase));
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.IsTrue(body.Contains("<html"), "Expected HTML content from /hash/");
    }

    [TestMethod]
    public async Task Hash_PutAndGetById_HappyPath()
    {
        var id = Guid.NewGuid();
        // prepare a deterministic 32-byte payload
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var base64 = Convert.ToBase64String(bytes);

        using (var http = new HttpClient())
        {
            var putResp = await http.PutAsync($"{ServiceBaseUrl}hash/{id}", new StringContent(base64)).ConfigureAwait(false);
            putResp.EnsureSuccessStatusCode();
            var returned = await putResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.AreEqual(base64, returned);

            var getResp = await http.GetAsync($"{ServiceBaseUrl}hash/{id}").ConfigureAwait(false);
            getResp.EnsureSuccessStatusCode();
            var got = await getResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.AreEqual(base64, got);
            Assert.AreEqual("text/plain", getResp.Content.Headers.ContentType?.MediaType);
        }
    }

    [TestMethod]
    public async Task Hash_GetById_NotFound()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient();
        var resp = await http.GetAsync($"{ServiceBaseUrl}hash/{id}").ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Hash_Put_EmptyBody_Returns400()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient();
        var resp = await http.PutAsync($"{ServiceBaseUrl}hash/{id}", new StringContent("")).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Hash_Put_ConflictOnDuplicate()
    {
        var id = Guid.NewGuid();
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var base64 = Convert.ToBase64String(bytes);

        using var http = new HttpClient();
        var first = await http.PutAsync($"{ServiceBaseUrl}hash/{id}", new StringContent(base64)).ConfigureAwait(false);
        first.EnsureSuccessStatusCode();

        var second = await http.PutAsync($"{ServiceBaseUrl}hash/{id}", new StringContent(base64)).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

}
