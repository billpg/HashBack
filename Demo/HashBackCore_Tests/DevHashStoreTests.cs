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
            var storeReturnValue = DevHashStore.Store(
                "MyUser", "MyFilename", 
                Convert.FromBase64String("I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A="));
            Assert.AreEqual(
                "A04C3D1BEAA1CEF5D316769AE548F06AB8A4A5A70D3110AA0E119E3152A3E3B4" +
                "/" +
                "D6F1B69FA7A6248F4503E4F59B79807001818D5A78F133AA2720FABCE4B351BC" +
                ".txt", storeReturnValue);
            var loaded = DevHashStore.Load(
                storeReturnValue.Substring(0, 64), 
                storeReturnValue.Substring(65, 64));
            Assert.AreEqual(
                "I/Ran/PBKDF2/On/My/JSON/and/this/resulted/A=", 
                Convert.ToBase64String(loaded));
        }

    }
}
