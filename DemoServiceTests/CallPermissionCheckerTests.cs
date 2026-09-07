using System;
using System.Net;
using System.Threading.Tasks;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class CallPermissionCheckerTests
{
    private static ServiceData GetServiceData()
        => new ServiceData { ConfigServiceHost = $"{Guid.NewGuid()}.example" };

    /// <summary>A fake IHttpGetter that counts calls and can be told to throw, for testing the cache and failure handling.</summary>
    private sealed class CountingHttpGetter : IHttpGetter
    {
        public int CallCount { get; private set; }
        public Uri? LastUrl { get; private set; }
        public SimpleHttpResponse? Response { get; set; }
        public Exception? ThrowOnGet { get; set; }

        public Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req, Action<IPAddress>? onResolved = null)
        {
            CallCount++;
            LastUrl = req.Url;
            onResolved?.Invoke(IPAddress.Loopback);
            if (ThrowOnGet != null)
                throw ThrowOnGet;
            return Task.FromResult(Response ?? new SimpleHttpResponse(404));
        }
    }

    [TestMethod]
    public async Task IsCallPermitted_SelfCall_AlwaysAllowedWithoutFetching()
    {
        var serviceData = GetServiceData();
        var getter = new CountingHttpGetter();
        var checker = new CallPermissionChecker(getter, serviceData);

        bool allowed = await checker.IsCallPermittedAsync(new Uri($"https://{serviceData.ConfigServiceHost}/hello/"));

        Assert.IsTrue(allowed);
        Assert.AreEqual(0, getter.CallCount, "Calling ourselves shouldn't need a permission fetch at all.");
    }

    [TestMethod]
    public async Task IsCallPermitted_TargetGrantsPermission_ReturnsTrue()
    {
        var getter = new CountingHttpGetter
        {
            Response = new SimpleHttpResponse(200).WithBody("{\"allow\": true}")
        };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        Assert.IsTrue(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_TargetExplicitlyRefuses_ReturnsFalse()
    {
        var getter = new CountingHttpGetter
        {
            Response = new SimpleHttpResponse(200).WithBody("{\"allow\": false}")
        };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://parsnip.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_NoFileAt404_ReturnsFalse()
    {
        var getter = new CountingHttpGetter { Response = new SimpleHttpResponse(404) };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://swede.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_MalformedJson_ReturnsFalse()
    {
        var getter = new CountingHttpGetter
        {
            Response = new SimpleHttpResponse(200).WithBody("this is not JSON")
        };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_FetchThrows_ReturnsFalse()
    {
        var getter = new CountingHttpGetter { ThrowOnGet = new BadRequestException("External URL not available.", "boom") };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        Assert.IsFalse(await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/xyz")));
    }

    [TestMethod]
    public async Task IsCallPermitted_FetchesTheWellKnownPathOnTheTargetsOwnHost()
    {
        var getter = new CountingHttpGetter { Response = new SimpleHttpResponse(200).WithBody("{\"allow\": true}") };
        var checker = new CallPermissionChecker(getter, GetServiceData());

        await checker.IsCallPermittedAsync(new Uri("https://rutabaga.example/some/path?query=1"));

        // The checker should have asked rutabaga.example's own well-known path, not the
        // original target path.
        Assert.AreEqual(1, getter.CallCount);
        Assert.AreEqual(new Uri("https://rutabaga.example/.well-known/demo-hashback-dev.json"), getter.LastUrl);
    }

    [TestMethod]
    public async Task IsCallPermitted_SecondCallWithinCacheWindow_DoesNotReFetch()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var getter = new CountingHttpGetter { Response = new SimpleHttpResponse(200).WithBody("{\"allow\": true}") };
        var checker = new CallPermissionChecker(getter, GetServiceData(), () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        Assert.IsTrue(await checker.IsCallPermittedAsync(target));
        Assert.IsTrue(await checker.IsCallPermittedAsync(target));
        Assert.AreEqual(1, getter.CallCount, "The second call within the cache window shouldn't re-fetch.");
    }

    [TestMethod]
    public async Task IsCallPermitted_AfterPositiveCacheExpires_ReFetches()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var getter = new CountingHttpGetter { Response = new SimpleHttpResponse(200).WithBody("{\"allow\": true}") };
        var checker = new CallPermissionChecker(getter, GetServiceData(), () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target);
        now = now.Add(CallPermissionChecker.PositiveCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target);

        Assert.AreEqual(2, getter.CallCount, "Should re-check once the positive cache entry has expired.");
    }

    [TestMethod]
    public async Task IsCallPermitted_ARefusalCachesForTheShorterNegativeDuration()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var getter = new CountingHttpGetter { Response = new SimpleHttpResponse(404) };
        var checker = new CallPermissionChecker(getter, GetServiceData(), () => now);
        var target = new Uri("https://rutabaga.example/xyz");

        await checker.IsCallPermittedAsync(target);
        now = now.Add(CallPermissionChecker.NegativeCacheDuration).AddSeconds(1);
        await checker.IsCallPermittedAsync(target);

        Assert.AreEqual(2, getter.CallCount, "A refusal should re-check sooner than a grant would.");
    }
}
