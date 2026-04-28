using billpg.HashBackDemoService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HashBackDemoServiceTests;

[TestClass]
public class BoudicaTests
{
    [TestMethod]
    public async Task Boudica_NormalRequest()
    {
        var boudica = Boudica.Start(0, MyRequestHandler);
        BoudicaRequest? capturedRequest = null;
        BoudicaResponse MyRequestHandler(BoudicaRequest req)
        {
            capturedRequest = req;
            return BoudicaResponse.Create()
                .WithStatus(200, "OKay")
                .WithHeader("X-billpg", "industries")
                .WithHeader("Content-Type", "text/plain")
                .WithBody("Rutabaga");
        }

        using var http = new HttpClient();
        var host = $"localhost:{boudica.ListenPort}";
        var resp = await http.GetAsync($"http://{host}/");
        await boudica.Stop();

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("GET", capturedRequest.HttpMethod);
        Assert.AreEqual("/", capturedRequest.Resource);
        Assert.AreEqual("HTTP/1.1", capturedRequest.HttpVersion);
        Assert.AreEqual(host, capturedRequest.HeaderDict["Host"]);
        Assert.IsNull(capturedRequest.RequestBody);

        Assert.AreEqual(200, (int)resp.StatusCode);
        Assert.AreEqual("OKay", resp.ReasonPhrase);
        Assert.AreEqual("industries", resp.Headers.Where(h => h.Key == "X-billpg").Single().Value.Single());
        Assert.AreEqual("Rutabaga", await resp.Content.ReadAsStringAsync());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Boudica_ThrowsOnBadPort()
    {        
        Boudica.Start(int.MaxValue / 2, ThrowException);
        static BoudicaResponse ThrowException(BoudicaRequest req)
            => throw new NotImplementedException();
    }
}
