using billpg.CrossRequestTokenExchange;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrteTests
{
    [TestClass]
    public class CommonHelperTests
    {
        [TestMethod]
        public void GenerateInitiatorKey_Length()
            => Assert.AreEqual(47, CommonHelpers.GenerateInitiatorKey().Length);

        [TestMethod]
        public void CalculateHashKey_Rutabagax4()
            => Assert.AreEqual(
                "IOZzTxoVXh/CuvrpU5cyuOUBkG7cxj8q2fJb0pWbgIs=",
                Convert.ToBase64String(
                    CommonHelpers.CalculateHashKey(
                        "RutabagaRutabagaRutabagaRutabaga")
                    .ToArray()));

        [TestMethod]
        public void CalculateHashKey_EmptyString()
            => Assert.AreEqual(
                "VPOEuDAt80uWrc/WwUgbio1hSJI3AmITETDyyaVxVAE=",
                Convert.ToBase64String(
                    CommonHelpers.CalculateHashKey("")
                    .ToArray()));

        [TestMethod]
        public void CalculateHashKey_TwoGuids()
            => Assert.AreEqual(
                "lWshNzAmK+JSM8sEBfs26m4tLmrHLR6yDy+M4rRFR0A=",
                Convert.ToBase64String(
                    CommonHelpers.CalculateHashKey(
                        "CDCD7723-127D-4592-A4A3-46409BB0B54E" +
                        "A26C1188-7A1F-4327-9902-E6182EEADB4F"
                        ).ToArray()));
    }
}
