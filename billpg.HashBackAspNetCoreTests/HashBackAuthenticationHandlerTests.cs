using billpg.HashBackAspNetCore;
using billpg.HashBackCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;

namespace billpg.HashBackAspNetCoreTests;

[TestClass]
public class HashBackAuthenticationHandlerTests
{
    private static TestServer BuildServer(Action<HashBackAuthenticationOptions> configureOptions)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAuthentication(HashBackAuthenticationDefaults.AuthenticationScheme)
                        .AddHashBack(configureOptions);
                });
                webBuilder.Configure(app =>
                {
                    app.UseAuthentication();
                    app.Run(async ctx =>
                    {
                        var result = await ctx.AuthenticateAsync();
                        if (!result.Succeeded)
                        {
                            await ctx.ChallengeAsync();
                            return;
                        }
                        string user = result.Principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                        await ctx.Response.WriteAsync(user);
                    });
                });
            })
            .Start();
        return host.GetTestServer();
    }

    [TestMethod]
    public async Task NoHeader_Returns401WithHashBackChallenge()
    {
        using var server = BuildServer(options =>
        {
            options.Realm = "rutabaga.example";
            options.Policy.RequireHost("rutabaga.example");
            options.Policy.SetSyncIdentifyUser(verify => verify.Host);
        });
        using var client = server.CreateClient();

        var resp = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        StringAssert.Contains(resp.Headers.WwwAuthenticate.ToString(), "HashBack");
        StringAssert.Contains(resp.Headers.WwwAuthenticate.ToString(), "rutabaga.example");
    }

    [TestMethod]
    public async Task ValidRequest_AuthenticatesAsIdentifiedUser()
    {
        string publishedHash = string.Empty;
        using var server = BuildServer(options =>
        {
            options.Policy.RequireHost("rutabaga.example");
            options.Policy.RequireNowWindow(30);
            options.Policy.SetSyncIdentifyUser(verify => verify.Host);
            options.UseDefaultVerificationHashFetcher = false;
            options.Policy.SetSyncGetVerificationHash(_ => publishedHash);
        });
        using var client = server.CreateClient();

        var request = HashBackRequest.Create("rutabaga.example", new Uri("https://parsnip.example/hashback"));
        publishedHash = request.VerificationHash;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("HashBack", request.AuthToken);
        var resp = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("parsnip.example", await resp.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task WrongHost_Returns401()
    {
        using var server = BuildServer(options =>
        {
            options.Policy.RequireHost("rutabaga.example");
            options.Policy.RequireNowWindow(30);
            options.Policy.SetSyncIdentifyUser(verify => verify.Host);
            options.UseDefaultVerificationHashFetcher = false;
            options.Policy.SetSyncGetVerificationHash(_ => "irrelevant");
        });
        using var client = server.CreateClient();

        var request = HashBackRequest.Create("swede.example", new Uri("https://parsnip.example/hashback"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("HashBack", request.AuthToken);
        var resp = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [TestMethod]
    public async Task WrongVerificationHash_Returns401()
    {
        using var server = BuildServer(options =>
        {
            options.Policy.RequireHost("rutabaga.example");
            options.Policy.RequireNowWindow(30);
            options.Policy.SetSyncIdentifyUser(verify => verify.Host);
            options.UseDefaultVerificationHashFetcher = false;
            options.Policy.SetSyncGetVerificationHash(_ => "not-the-right-hash");
        });
        using var client = server.CreateClient();

        var request = HashBackRequest.Create("rutabaga.example", new Uri("https://parsnip.example/hashback"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("HashBack", request.AuthToken);
        var resp = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
