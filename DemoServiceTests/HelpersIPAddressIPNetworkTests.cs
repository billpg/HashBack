using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;

namespace DemoServiceTests;

[TestClass]
public class HelpersIPAddressIPNetworkTests
{
    [TestMethod]
    public void IPAddress_IsIPv4AndIsIPv6_ExtensionsWork()
    {
        var ipv4 = IPAddress.Parse("192.0.2.1");
        var ipv6 = IPAddress.Parse("2001:db8::1");

        Assert.IsTrue(ipv4.IsIPv4(), "Expected IPv4 address to report IsIPv4()");
        Assert.IsFalse(ipv4.IsIPv6(), "Expected IPv4 address to report not IsIPv6()");
        Assert.IsTrue(ipv6.IsIPv6(), "Expected IPv6 address to report IsIPv6()");
        Assert.IsFalse(ipv6.IsIPv4(), "Expected IPv6 address to report not IsIPv4()");
    }

    [TestMethod]
    public void Networks_ForIPv4_ReturnsExpectedPrefixRangeAndCounts()
    {
        var ip = IPAddress.Parse("192.0.2.5");
        var nets = Helpers.Networks(ip);

        // IPv4: prefixes from /32 down to /8 inclusive => 25 entries
        Assert.AreEqual(4, nets.Count, "Helpers.Networks(IPv4) should always return 4.");

        // First should be /32, last should be /8
        Assert.AreEqual(32, nets[0].PrefixLength, "First IPv4 network should be /32");
        Assert.AreEqual(8, nets[nets.Count - 1].PrefixLength, "Last IPv4 network should be /8");

        // Extension helpers on IPNetwork
        Assert.IsTrue(nets[0].IsIPv4(), "Network based on IPv4 should report IsIPv4()");
        Assert.IsFalse(nets[0].IsIPv6(), "Network based on IPv4 should not report IsIPv6()");
    }

    [TestMethod]
    public void Networks_ForIPv6_ReturnsExpectedPrefixRangeAndCounts()
    {
        var ip = IPAddress.Parse("2001:db8::1");
        var nets = Helpers.Networks(ip);

        // IPv6: prefixes from /64 down to /16 stepping by 4 => (64-16)/4 + 1 = 13 entries
        Assert.AreEqual(4, nets.Count, "Helpers.Networks(IPv6) should always return 4.");

        // First should be /64, last should be /16
        Assert.AreEqual(64, nets[0].PrefixLength, "First IPv6 network should be /64");
        Assert.AreEqual(16, nets[nets.Count - 1].PrefixLength, "Last IPv6 network should be /16");

        // Extension helpers on IPNetwork
        Assert.IsTrue(nets[0].IsIPv6(), "Network based on IPv6 should report IsIPv6()");
        Assert.IsFalse(nets[0].IsIPv4(), "Network based on IPv6 should not report IsIPv4()");
    }

    [TestMethod]
    public void Weight_ForIPv4AndIPv6_ComputesExpectedValues()
    {
        var ipv4 = IPAddress.Parse("198.51.100.17");
        var ipv4Nets = Helpers.Networks(ipv4);
        // /32 weight -> 32, /8 weight -> 8
        Assert.AreEqual(1, ipv4Nets[0].Weight(), "IPv4 /32 weight should equal 1");
        Assert.AreEqual(4, ipv4Nets[ipv4Nets.Count - 1].Weight(), "IPv4 /8 weight should equal 4");

        var ipv6 = IPAddress.Parse("2001:db8:abcd::1");
        var ipv6Nets = Helpers.Networks(ipv6);
        // IPv6 weight uses PrefixLength/2, so /64 -> 32, /16 -> 8
        Assert.AreEqual(1, ipv6Nets[0].Weight(), "IPv6 /64 weight should equal 1");
        Assert.AreEqual(4, ipv6Nets[ipv6Nets.Count - 1].Weight(), "IPv6 /16 weight should equal 4");
    }

    [TestMethod]
    public void Networks_ExpectedValues_IPv4()
    {
        var actual = Helpers.Networks(IPAddress.Parse("123.45.67.89"));
        var expected = new List<string> { "123.45.67.89/32", "123.45.67.0/24", "123.45.0.0/16", "123.0.0.0/8" };
        CollectionAssert.AreEqual(expected, actual.Select(net => net.ToString()).ToList());
        CollectionAssert.AreEqual(new List<int> { 1,2,3,4 }, actual.Select(Helpers.Weight).ToList());
    }


    [TestMethod]
    public void Networks_ExpectedValues_IPv6()
    {
        var actual = Helpers.Networks(IPAddress.Parse("1234:5678:90ab:cdef:fedc:ba09:8765:4321"));
        var expected = new List<string> { "1234:5678:90ab:cdef::/64", "1234:5678:90ab::/48", "1234:5678::/32", "1234::/16" };
        CollectionAssert.AreEqual(expected, actual.Select(net => net.ToString()).ToList());
        CollectionAssert.AreEqual(new List<int> { 1,2,3,4 }, actual.Select(Helpers.Weight).ToList());
    }
}
