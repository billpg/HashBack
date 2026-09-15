using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Reflection;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public class IpFilterTests
{
    [TestMethod]
    public void Rejects_NullOrLoopback_ReturnsFalse()
    {
        var filter = new IpFilter();

        Assert.IsFalse(filter.IsAcceptable(null));
        Assert.IsFalse(filter.IsAcceptable(IPAddress.Loopback));
        Assert.IsFalse(filter.IsAcceptable(IPAddress.IPv6Loopback));
    }

    [TestMethod]
    public void Rejects_ReservedRanges_ReturnsFalse()
    {
        var filter = new IpFilter();

        Assert.IsFalse(filter.IsAcceptable(IPAddress.Parse("10.1.2.3")));
        Assert.IsFalse(filter.IsAcceptable(IPAddress.Parse("192.168.1.5")));
        Assert.IsFalse(filter.IsAcceptable(IPAddress.Parse("127.0.0.1")));
    }

    [TestMethod]
    public void Allows_UntilQuotaExhausted_ForSingleIPv4Address()
    {
        var filter = new IpFilter();
        var ip = IPAddress.Parse("123.45.67.89");

        int allowed = 0;
        // The implementation sets the /32 quota based on Helpers.Weight = 1 => 1^10 * 100 = 100
        for (int i = 0; i < 150; i++)
        {
            if (filter.IsAcceptable(ip))
                allowed++;
            else
                break;
        }

        Assert.AreEqual(82, allowed, "The most-specific (/32) quota should allow 82 requests then reject.");
    }

    [TestMethod]
    public void DifferentIpWithDistinct32StillAllowed_AfterOtherIpExhausted()
    {
        var filter = new IpFilter();
        var ip1 = IPAddress.Parse("123.45.67.89");
        var ip2 = IPAddress.Parse("123.45.67.90");

        // Exhaust ip1's /32 quota
        for (int i = 0; i < 101; i++)
            filter.IsAcceptable(ip1);

        // ip2 has its own /32 quota and should still be accepted on first attempt
        Assert.IsTrue(filter.IsAcceptable(ip2));
    }

    [TestMethod]
    public void Accepts_ValidIPv6_NotInRejectRanges()
    {
        var filter = new IpFilter();
        var ip6 = IPAddress.Parse("2001:db8::1"); // documentation range, not in reject list

        Assert.IsTrue(filter.IsAcceptable(ip6));
    }

    [TestMethod]
    public void CombinedLoad_Exhausts24Quota_WhileIndividualIPsRemainWithinTheir32Quota()
    {
        /* Start a new filter with the default state. */
        var filter = new IpFilter();

        /* Loop through the many 4th-byte values for an IPv4. */
        int totalCount = 0;
        for (int lastByte = 1; lastByte < 255; lastByte++)
        {
            /* Construct a new IPv4 in the same /24 block and ask if
             * it is acceptable 50, keeping below each single IP's
             * quota, but counting the total times. A single /24 block
             * has a quota of 731. */
            var ip = IPAddress.Parse($"123.45.67.{lastByte}");
            for (int attempt = 0; attempt < 50; attempt++)
                Assert.AreEqual(++totalCount < 731, filter.IsAcceptable(ip));
        }
    }

    [TestMethod]
    public void Quota_Expires_AllowsNewRequests_AfterUtcNowAdvance()
    {
        // Arrange: controllable clock
        var filter = new IpFilter();
        DateTime clock = DateTime.UtcNow;
        filter.UtcNow = () => clock;

        var ip = IPAddress.Parse("123.45.67.89");

        // Act: exhaust the current quotas for this IP (loop until IsAcceptable returns false)
        int allowedBefore = 0;
        while (filter.IsAcceptable(ip))
            allowedBefore++;

        Assert.IsTrue(allowedBefore > 0, "Should allow at least one request before quota exhaustion.");
        Assert.IsFalse(filter.IsAcceptable(ip), "After exhaustion further calls should be rejected.");

        // Advance clock past the quota duration (quotaDuration is 1 hour in implementation)
        clock = clock.AddHours(2);

        // After expiry, a new quota should be created and the IP should be allowed again
        bool allowedAfterExpiry = filter.IsAcceptable(ip);
        Assert.IsTrue(allowedAfterExpiry, "After quota expiry the same IP should be allowed again (new quota created).");
    }
}
