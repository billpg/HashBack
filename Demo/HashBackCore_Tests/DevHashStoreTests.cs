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
            DevHashStore.Store("MyUser", 12345, "I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A=");
            var loaded = DevHashStore.Load("MyUser", 12345);
            Assert.AreEqual("I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A=", loaded);
        }

    }
}
