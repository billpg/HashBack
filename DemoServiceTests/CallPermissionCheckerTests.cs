using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using billpg.SpartanHttpClient;
using DemoService;
using DemoService.Data;
using DemoService.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DemoServiceTests;

[TestClass]
public sealed class CallPermissionCheckerTests
{
    private static readonly IPAddress DefaultCallerIp = IPAddress.Parse("203.0.113.9");

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

    /// <summary>A fake IOutboundGetLog reporting a fixed count for every host, for testing
    /// GetsPerHour enforcement without a real database.</summary>
    private sealed class FixedCountOutboundGetLog : IOutboundGetLog
    {
        private readonly int count;
        public FixedCountOutboundGetLog(int count) => this.count = count;
        public Task LogAsync(IPAddress callerIp, OutboundGetSource source, Uri target) => Task.CompletedTask;
        public Task<int> CountRecentGetsAsync(string targetHost, DateTime since) => Task.FromResult(count);
    }

    /// <summary>Builds a real (but otherwise empty) DI container exposing just the given
    /// IOutboundGetLog, so CallPermissionChecker's IServiceScopeFactory dependency has
    /// something genuine to resolve from.</summary>
    private static IServiceScopeFactory BuildScopeFactory(IOutboundGetLog? outboundGetLog = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(outboundGetLog ?? new NoOpOutboundGetLog());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static CallPermissionChecker BuildChecker(
        ServiceData serviceData, ISpartanEngine engine, IOutboundGetLog? outboundGetLog = null, Func<DateTime>? utcNow = null)
        => new(serviceData, engine, BuildScopeFactory(outboundGetLog), utcNow);

    /// <summary>Builds a checker with a grant pre-seeded directly into its cache - bypassing
    /// any fetch entirely - for tests that only care about the cached-decision logic, not
    /// the network fetch itself. The seeded grant is addressed to the checker's own
    /// ServiceData.ConfigServiceHost, matching what a real, valid grant would say.</summary>
    private (CallPermissionChecker checker, ServiceData serviceData) BuildTestPermissionChecker(
        string populateForTarget, bool grantPermission = true, Action<PermitGrant>? configureGrant = null, IOutboundGetLog? outboundGetLog = null)
    {
        var serviceData = GetServiceData();
        var checker = BuildChecker(serviceData, new CountingSpartanEngine(), outboundGetLog);
        var grant = new PermitGrant { GetPermissionGrantedTo = serviceData.ConfigServiceHost };
        configureGrant?.Invoke(grant);
        checker.PopulateGrantCache(populateForTarget, grantPermission ? grant : null);
        return (checker, serviceData);
    }

    [TestMethod]
    public async Task IsCallPermitted_TargetGrantsPermission_ReturnsTrue()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example");
        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_NoGrantCached_ReturnsFalse()
    {
        var (checker, _) = BuildTestPermissionChecker("parsnip.example", grantPermission: false);
        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://parsnip.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_NoFileAt404_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine { Response = new SpartanResponse().WithStatusCode(404) };
        var checker = BuildChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://swede.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_MalformedJson_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine
        {
            Response = new SpartanResponse().WithStatusCode(200).WithBody("this is not JSON")
        };
        var checker = BuildChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
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
        var checker = BuildChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_FetchThrows_ReturnsFalse()
    {
        var engine = new CountingSpartanEngine { ThrowOnGet = new BadRequestException("External URL not available.", "boom") };
        var checker = BuildChecker(GetServiceData(), engine);

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
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
        var checker = BuildChecker(serviceData, engine);

        await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/some/path?query=1"), DefaultCallerIp);

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
        var checker = BuildChecker(serviceData, engine, utcNow: () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        Assert.IsTrue(await checker.IsCallPermittedAsync(target, DefaultCallerIp));
        Assert.IsTrue(await checker.IsCallPermittedAsync(target, DefaultCallerIp));
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
        var checker = BuildChecker(serviceData, engine, utcNow: () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target, DefaultCallerIp);
        now = now.Add(CallPermissionChecker.PositiveCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target, DefaultCallerIp);

        Assert.AreEqual(2, engine.CallCount, "Should re-check once the positive cache entry has expired.");
    }

    [TestMethod]
    public async Task IsCallPermitted_ARefusalCachesForTheShorterNegativeDuration()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new CountingSpartanEngine { Response = new SpartanResponse().WithStatusCode(404) };
        var checker = BuildChecker(GetServiceData(), engine, utcNow: () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target, DefaultCallerIp);
        now = now.Add(CallPermissionChecker.NegativeCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target, DefaultCallerIp);

        Assert.AreEqual(2, engine.CallCount, "A refusal should re-check sooner than a grant would.");
    }

    [TestMethod]
    public async Task IsCallPermitted_CallerIpMatchesBareAddress_ReturnsTrue()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.CallerIP = new List<string> { DefaultCallerIp.ToString() });

        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_CallerIpMatchesCidrRange_ReturnsTrue()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.CallerIP = new List<string> { "203.0.113.0/24" });

        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_CallerIpOutsideGrantedRange_ReturnsFalse()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.CallerIP = new List<string> { "198.51.100.0/24" });

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_UrlWithinGrantedPrefix_ReturnsTrue()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.Url = new List<string> { "/api/hashback/" });

        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/api/hashback/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_UrlOutsideGrantedPrefix_ReturnsFalse()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.Url = new List<string> { "/api/hashback/" });

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/somewhere-else"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_UnderGetsPerHourLimit_ReturnsTrue()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.GetsPerHour = 10,
            outboundGetLog: new FixedCountOutboundGetLog(9));

        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }

    [TestMethod]
    public async Task IsCallPermitted_AtGetsPerHourLimit_ReturnsFalse()
    {
        var (checker, _) = BuildTestPermissionChecker("rutabaga.example",
            configureGrant: g => g.GetsPerHour = 10,
            outboundGetLog: new FixedCountOutboundGetLog(10));

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz"), DefaultCallerIp));
    }
}
