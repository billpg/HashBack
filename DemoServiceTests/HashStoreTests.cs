using System;
using System.Net;
using System.Threading.Tasks;
using DemoService.Data;

namespace DemoServiceTests;

[TestClass]
public sealed class HashStoreTests
{
    [TestMethod]
    public async Task TryAddHash_ReuseWithinBlockWindow_IsRejected()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new HashStore(testDb.Db, () => now);
        var id = Guid.NewGuid();

        Assert.IsTrue(await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback));

        // Well within the reuse-block window (a day), and also well past the much shorter
        // retrieval window (an hour) - reuse should still be refused either way.
        now = now.AddHours(2);
        Assert.IsFalse(await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback),
            "Reuse should be blocked long after the retrieval window closes, not just during it.");
    }

    [TestMethod]
    public async Task TryAddHash_ReuseAfterBlockWindowElapses_IsAllowed()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new HashStore(testDb.Db, () => now);
        var id = Guid.NewGuid();
        var originalBytes = new byte[32];
        var newBytes = Enumerable.Repeat((byte)0xFF, 32).ToArray();

        Assert.IsTrue(await store.TryAddHashAsync(id, originalBytes, IPAddress.Loopback));

        now = now.Add(HashStore.ReuseBlockWindow).AddSeconds(1);
        Assert.IsTrue(await store.TryAddHashAsync(id, newBytes, IPAddress.Loopback),
            "Reuse should be allowed once the full reuse-block window has elapsed.");

        var fetched = await store.TryGetHashAsync(id, IPAddress.Loopback, "");
        Assert.IsNotNull(fetched);
        Assert.AreEqual(Convert.ToBase64String(newBytes), fetched!.HashAsString,
            "The overwritten value should be the new one, not the original.");
    }

    [TestMethod]
    public async Task TryGetHash_PastRetrievalWindow_ReturnsNullButStillBlocksReuse()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new HashStore(testDb.Db, () => now);
        var id = Guid.NewGuid();

        Assert.IsTrue(await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback));

        now = now.Add(HashStore.RetrievalWindow).AddMinutes(1);
        Assert.IsNull(await store.TryGetHashAsync(id, IPAddress.Loopback, ""),
            "Should no longer be retrievable once the (short) retrieval window has passed.");
        Assert.IsFalse(await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback),
            "But the id should still be blocked from reuse - the retrieval window and the reuse-block window are different things.");
    }

    [TestMethod]
    public async Task TryGetHash_ExceedsMaxGetCount_ReturnsNull()
    {
        using var testDb = new TestHashDb();
        var store = new HashStore(testDb.Db);
        var id = Guid.NewGuid();
        await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback);

        for (int i = 0; i < HashStore.MaxGetCount; i++)
            Assert.IsNotNull(await store.TryGetHashAsync(id, IPAddress.Loopback, ""), $"GET #{i + 1} should still succeed.");

        Assert.IsNull(await store.TryGetHashAsync(id, IPAddress.Loopback, ""),
            $"GET #{HashStore.MaxGetCount + 1} should be refused - the cap is {HashStore.MaxGetCount}.");
    }

    [TestMethod]
    public async Task ListGetHashEvents_LogsEachSuccessfulRetrieval()
    {
        using var testDb = new TestHashDb();
        var store = new HashStore(testDb.Db);
        var id = Guid.NewGuid();
        await store.TryAddHashAsync(id, new byte[32], IPAddress.Loopback);

        await store.TryGetHashAsync(id, IPAddress.Parse("203.0.113.7"), "User-Agent: rutabaga-tester\r\n");
        await store.TryGetHashAsync(id, IPAddress.Parse("203.0.113.8"), "User-Agent: parsnip-tester\r\n");

        var events = await store.ListGetHashEventsAsync(id);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(IPAddress.Parse("203.0.113.7"), events[0].GotBy);
        Assert.AreEqual(IPAddress.Parse("203.0.113.8"), events[1].GotBy);
    }
}
