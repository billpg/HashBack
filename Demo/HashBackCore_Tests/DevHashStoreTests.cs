using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCore_Tests
{
    [TestClass]
    public class DevHashStoreTests
    {
        [TestMethod]
        public void DevHashStore_RoundTrip()
        {            
            DevHashStore.Store("MyUser", "MyFilename", "I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A=");
            var loaded = DevHashStore.Load(
                "EA2F1834F3292C22750DA3CAEFD1181EFA94B3BF3021C46496A44FCE35EAAED3",
                "3459EBE16B97F1830A8A8EF4D82D7EDFDDED73C91EE49B902CCB959B6C40403F");
            Assert.AreEqual("I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A=", loaded);
        }

    }
}
