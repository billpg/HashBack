using System;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HashBackCoreTests
{
    [TestClass]
    public class DateTimeExtensionsTests
    {
        [TestMethod]
        public void ToUnixTimeSeconds_EpochUtc_ReturnsZero()
        {
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, dt.ToUnixTimeSeconds());
        }

        [TestMethod]
        public void ToUnixTimeSeconds_KnownValueUtc()
        {
            // 2000-01-01T00:00:00Z = 946684800 seconds since epoch
            var dt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(946684800, dt.ToUnixTimeSeconds());
        }

        [TestMethod]
        public void ToUnixTimeSeconds_RoundTrip()
        {
            var now = DateTime.UtcNow;
            long unix = now.ToUnixTimeSeconds();
            var roundTrip = DateTime.UnixEpoch.AddSeconds(unix);
            Assert.AreEqual(unix, roundTrip.ToUnixTimeSeconds());
        }

        [TestMethod]
        public void ToUnixTimeSeconds_UtcKind_DoesNotThrow()
        {
            var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // Should not throw
            _ = dt.ToUnixTimeSeconds();
        }

        [TestMethod]
        public void ToUnixTimeSeconds_LocalKind_ConvertsToUtcCorrectly()
        {
            var local = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
            var utc = local.ToUniversalTime();
            long expected = (long)(utc - DateTime.UnixEpoch).TotalSeconds;
            long actual = local.ToUnixTimeSeconds();
            Assert.AreEqual(expected, actual, "Local DateTime should be converted to UTC and produce correct Unix time.");
        }

        [TestMethod]
        public void ToUnixTimeSeconds_UnspecifiedKind_ThrowsArgumentException()
        {
            var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.ThrowsException<ArgumentException>(() => dt.ToUnixTimeSeconds());
        }
    }
}