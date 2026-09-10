using System;
using System.Threading.Tasks;
using billpg.SpartanHttpClient;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class CallPermissionCheckerTests
{
    private static ServiceData GetServiceData()
        => new ServiceData { ConfigServiceHost = $"{Guid.NewGuid()}.example" };

    /// <summary>A fake ISpartanEngine that counts calls and can be told to throw or return
    /// a canned response, for testing CallPermissionChecker's fetch/cache/failure handling
    /// without any real network access.</summary>
    private sealed class CountingSpartanEngine : ISpartanEngine
    {
        public int CallCount { get; private set; }
        public Uri? LastUrl { get; private set; }
        public SpartanResponse? Response { get; set; }
        public Exception? ThrowOnGet { get; set; }

        public SpartanRequest Request(Uri url)
            => new SpartanRequest(url).WithRunner((request, cancellationToken) =>
            {
                CallCount++;
                LastUrl = request.Url;
                if (ThrowOnGet != null)
                    throw ThrowOnGet;
                return Task.FromResult(Response ?? new SpartanResponse().WithStatusCode(404));
            });
    }

    /// <summary>Builds a checker with a grant pre-seeded directly into its cache - bypassing
    /// any fetch entirely - for tests that only care about the cached-decision logic, not
    /// the network fetch itself. The seeded grant is addressed to the checker's own
    /// ServiceData.ConfigServiceHost, matching what a real, valid grant would say.</summary>
    private CallPermissionChecker BuildTestPermissionChecker(string populateForTarget, bool grantPermission = true)
    {
        var serviceData = GetServiceData();
        var checker = new CallPermissionChecker(serviceData, new CountingSpartanEngine());
        var grant = new PermitGrant { GetPermissionGrantedTo = serviceData.ConfigServiceHost };
        checker.PopulateGrantCache(populateForTarget, grantPermission ? grant : null);
        return checker;
    }

    [TestMethod]
    public async Task IsCallPermitted_TargetGrantsPermission_ReturnsTrue()
    {
        var checker = BuildTestPermissionChecker("rutabaga.example");
        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_NoGrantCached_ReturnsFalse()
    {
        var checker = BuildTestPermissionChecker("parsnip.example", grantPermission: false);
        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://parsnip.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_NoFileAt404_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine { Response = new SpartanResponse().WithStatusCode(404) };
        var checker = new CallPermissionChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://swede.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_MalformedJson_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200).WithBody("this is not JSON")
        };
        var checker = new CallPermissionChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_GrantedToADifferentHost_ReturnsFalse()
    {
        /* A grant that parses fine but names some other host as the grantee - proves the
         * GetPermissionGrantedTo check applies on a fresh fetch, not just a cached one. */
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200)
                .WithBody("{\"GetPermissionGrantedTo\": \"someone-else.example\"}")
        };
        var checker = new CallPermissionChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_FetchThrows_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine { ThrowOnGet = new BadRequestException("External URL not available.", "boom") };
        var checker = new CallPermissionChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_FetchesTheWellKnownPathOnTheTargetsOwnHost()
    {
        var serviceData = GetServiceData();
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200)
                .WithBody($"{{\"GetPermissionGrantedTo\": \"{serviceData.ConfigServiceHost}\"}}")
        };
        var checker = new CallPermissionChecker(serviceData, engine);

        await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/some/path?query=1"));

        // The checker should have asked rutabaga.example's own well-known path, not the
        // original target path.
        Assert.AreEqual(1, engine.CallCount);
        Assert.AreEqual(new Uri("https://rutabaga.example/.well-known/demo-hashback-dev.json"), engine.LastUrl);
    }

    [TestMethod]
    public async Task IsCallPermitted_SecondCallWithinCacheWindow_DoesNotReFetch()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var serviceData = GetServiceData();
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200)
                .WithBody($"{{\"GetPermissionGrantedTo\": \"{serviceData.ConfigServiceHost}\"}}")
        };
        var checker = new CallPermissionChecker(serviceData, engine, () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        Assert.IsTrue(await checker.IsCallPermittedAsync(target));
        Assert.IsTrue(await checker.IsCallPermittedAsync(target));
        Assert.AreEqual(1, engine.CallCount, "The second call within the cache window shouldn't re-fetch.");
    }

    [TestMethod]
    public async Task IsCallPermitted_AfterPositiveCacheExpires_ReFetches()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var serviceData = GetServiceData();
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200)
                .WithBody($"{{\"GetPermissionGrantedTo\": \"{serviceData.ConfigServiceHost}\"}}")
        };
        var checker = new CallPermissionChecker(serviceData, engine, () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target);
        now = now.Add(CallPermissionChecker.PositiveCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target);

        Assert.AreEqual(2, engine.CallCount, "Should re-check once the positive cache entry has expired.");
    }

    [TestMethod]
    public async Task IsCallPermitted_ARefusalCachesForTheShorterNegativeDuration()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new CountingSpartanEngine { Response = new SpartanResponse().WithStatusCode(404) };
        var checker = new CallPermissionChecker(GetServiceData(), engine, () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target);
        now = now.Add(CallPermissionChecker.NegativeCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target);

        Assert.AreEqual(2, engine.CallCount, "A refusal should re-check sooner than a grant would.");
    }
}
