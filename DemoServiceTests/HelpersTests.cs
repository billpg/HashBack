using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;

namespace DemoServiceTests;

[TestClass]
public class HelpersTests
{
    [TestMethod]
    [DataRow("Cf-Connecting-Ip")]
    [DataRow("cf-ray")]
    [DataRow("CF-WARP-TAG-ID")]
    [DataRow("cf-")]
    public void IsCloudflareHeader_CfPrefixedHeaderAnyCase_ReturnsTrue(string headerName)
    {
        Assert.IsTrue(Helpers.IsCloudflareHeader(headerName));
    }

    [TestMethod]
    [DataRow("Host")]
    [DataRow("X-Forwarded-For")]
    [DataRow("Cdn-Loop")]
    [DataRow("Content-Type")]
    [DataRow("cf")]
    public void IsCloudflareHeader_NonCfHeader_ReturnsFalse(string headerName)
    {
        Assert.IsFalse(Helpers.IsCloudflareHeader(headerName));
    }
}
