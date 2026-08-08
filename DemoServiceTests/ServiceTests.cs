using Microsoft.AspNetCore.Components;
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
using System.Threading.Tasks;
using AuthenticationHeaderValue = System.Net.Http.Headers.AuthenticationHeaderValue;

namespace DemoServiceTests;

[TestClass]
public class ServiceTests
{
    private static Process? serviceProc = null;
    private const string ServiceBaseUrl = "http://localhost:9001/";

    [ClassInitialize]
    public static async Task LaunchService(TestContext testContext)
    {
        /* Kill any service processes left behind. */
        foreach (var proc in Process.GetProcessesByName("DemoService"))
            proc.Kill();

        /* Find the DemoServcie EXE and launch it. */
        var dllPath = typeof(DemoService.Helpers).Assembly.Location;
        var exePath = Path.ChangeExtension(dllPath, ".exe");
        var psi = new ProcessStartInfo(exePath);
        psi.CreateNoWindow = false;
        psi.WindowStyle = ProcessWindowStyle.Normal;
        serviceProc = Process.Start(exePath);
    }

    [ClassCleanup]
    public static void StopService()
    {
        if (serviceProc != null && !serviceProc.HasExited)
            serviceProc.Kill();
    }

    private JObject BuildClaim()
        => new JObject
        {
            ["Version"] = "BILLPG_DRAFT_4.2",
            ["Host"] = new Uri(ServiceBaseUrl).Authority,
            ["Now"] = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds,
            ["Unus"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            ["Verify"] = $"{ServiceBaseUrl}hash/{Guid.NewGuid()}"
        };

    [TestMethod]
    public async Task Hello_HappyPath()
    {
        /* Build a JSON claim. */
        var claimAsJson = BuildClaim();

        /* Hash the claim. */
        byte[] claimAsBytes = Encoding.ASCII.GetBytes(claimAsJson.ToString());
        byte[] salt = Convert.FromBase64String("MGrvPY28enVH8lmkmlksLxQqIvX65oseOPAoqCO4XPw=");
        byte[] hashInput = Enumerable.Concat(salt, claimAsBytes).ToArray();
        byte[] hashAsBytes = SHA256.HashData(hashInput);
        string hashAsText = Convert.ToBase64String(hashAsBytes);

        /* Upload the hash. */
        var httpPut = new HttpClient();
        string verifyUrl = claimAsJson["Verify"]!.Value<string>()!;
        var respPut = await httpPut.PutAsync(verifyUrl, new StringContent(hashAsText));
        respPut.EnsureSuccessStatusCode();

        /* Call the hello end-point. */
        HttpClient httpHello = new HttpClient();
        httpHello.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("HashBack", Convert.ToBase64String(claimAsBytes));
        var respHello = await httpHello.GetAsync($"{ServiceBaseUrl}hello/");
        respHello.EnsureSuccessStatusCode();
        var helloBody = await respHello.Content.ReadAsStringAsync();

        /* Check the response. */
        Assert.AreEqual("Hello localhost!", helloBody);
    }

    [TestMethod]
    public async Task Hello_NoAuth()
    {
        HttpClient httpHello = new HttpClient();
        var respHello = await httpHello.GetAsync($"{ServiceBaseUrl}hello/");
        Assert.AreEqual((HttpStatusCode)401, respHello.StatusCode);
        Assert.IsTrue(respHello.Headers.Contains("WWW-Authenticate"), "Response is missing WWW-Authenticate.");
        var authHeaders = respHello.Headers.GetValues("WWW-Authenticate").ToList();
        Assert.AreEqual(1, authHeaders.Count);
        Assert.AreEqual(
            "HashBack realm=\"demo.hashback.dev\" set-cookie=\"HashBackDemoService\"" +
            " version=\"BILLPG_DRAFT_4.2,BILLPG_DRAFT_4.1\"", authHeaders.Single());

        var helloBody = await respHello.Content.ReadAsStringAsync();
        Assert.IsTrue(helloBody.Contains("<html"));
    }

    [TestMethod]
    public async Task Hello_BadAuthorization()
    {
        HttpClient httpHello = new HttpClient();
        httpHello.DefaultRequestHeaders.Authorization 
            = new AuthenticationHeaderValue("HashBack", "ThisIsNotBase64EncodedJson==");
        var respHello = await httpHello.GetAsync($"{ServiceBaseUrl}hello/");
        Assert.AreEqual((HttpStatusCode)400, respHello.StatusCode);
        var errorText = await respHello.Content.ReadAsStringAsync();
    }
}
