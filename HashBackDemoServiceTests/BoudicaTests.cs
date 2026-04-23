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
    public async Task MyTestMethod()
    {
        var boudica = Boudica.Start(0, false, MyRequestHandler);
        BoudicaResponse MyRequestHandler(BoudicaRequest req)
        {
            return new BoudicaResponse();
        }

        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://localhost:{boudica.ListenPort}/");        

        await boudica.Stop();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Boudica_ThrowsOnBadPort()
    {        
        Boudica.Start(int.MaxValue / 2, false, ThrowException);
        static BoudicaResponse ThrowException(BoudicaRequest req)
            => throw new NotImplementedException();
    }
}
