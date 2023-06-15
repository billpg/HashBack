using billpg.CrossRequestTokenExchange;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace CrteTests
{
    [TestClass]
    public class FixedSaltTests
    {
        [TestMethod]
        public void FixedSaltAsCsv()
            => Assert.AreEqual(
                "23,55,182,143,125,16,83,246,39,\r\n" +
                "139,153,96,49,236,145,3,81,202,\r\n" +
                "122,60,159,170,218,198,177,207,58,\r\n" +
                "36,30,197,162,179,230,77,194,140,\r\n" +
                "173,233,5,25,166,25,61,139,84,\r\n" +
                "140,34,47,62,114,94,174,137,38,\r\n" +
                "50,112,244,193,184,107,18,255,152,\r\n" +
                "96,216,228,166,187,110,215,53,21,\r\n" +
                "22,166,57,226,216,171,252,16,127,\r\n" +
                "156,159,152,121,244,57,150,227,100,\r\n" +
                "135,218,33,59,219,248,106,9,109\r\n", 
                FixedPbkdf2Salt.FixedSaltAsCommaSeparatedDecimalBytes);

        [TestMethod]
        public void FixedSaltAsBase64()
            => Assert.AreEqual(
                "Fze2j30QU/Yni5lgMeyRA1HKejyfqtrGs" +
                "c86JB7ForPmTcKMrekFGaYZPYtUjCIvPn" +
                "JerokmMnD0wbhrEv+YYNjkprtu1zUVFqY" +
                "54tir/BB/nJ+YefQ5luNkh9ohO9v4aglt", 
                FixedPbkdf2Salt.FixedSaltAsBase64);

        [TestMethod]
        public void FixedSaltMatchesHardCoded()
            => CollectionAssert.AreEqual(
                FixedPbkdf2Salt.CalculateFixedSalt().ToArray(), 
                CommonHelpers.FixedPbkdf2Salt.ToArray());
    }
}