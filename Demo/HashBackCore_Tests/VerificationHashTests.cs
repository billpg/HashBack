using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace HashBackCore_Tests
{
    [TestClass]
    public class VerificationHashTests
    {
        private void TestExpectedHash(
            string version, 
            string typeOfResponse, 
            string issuerUrl, 
            long now, 
            string unus, 
            int rounds, 
            string verifyUrl, 
            string expectedVerificationHash)
        {
            /* Constrct the CallerRequest object per parameters. */
            var req = new IssuerService.Request
            {
                HashBack = version,
                TypeOfResponse = typeOfResponse,
                IssuerUrl = issuerUrl,
                Now = now,
                Unus = unus,
                Rounds = rounds,
                VerifyUrl = verifyUrl
            };

            /* Find the hash. */
            string actualHash = req.VerificationHash();

            /* Check its verification hash is as documented. */
            Assert.AreEqual(expectedVerificationHash, actualHash);
        }

        /// <summary>
        /// Test the first example from the public-draft-3.0 version.
        /// </summary>
        [TestMethod]
        public void Test30ExampleOne()
            => TestExpectedHash(               
                    version: "HASHBACK-PUBLIC-DRAFT-3-0",
                    typeOfResponse: "BearerToken",
                    issuerUrl: "https://issuer.example/api/generate_bearer_token",
                    now: 529297200,
                    unus: "iZ5kWQaBRd3EaMtJpC4AS40JzfFgSepLpvPxMTAbt6w=",
                    rounds: 1,
                    verifyUrl: "https://caller.example/hashback_files/my_json_hash.txt",
                    expectedVerificationHash: "2pFPaBO1bf6B7O8t9mCX8XZqU8rPtxcEYRU4eurPJEU=");

        /// <summary>
        /// Test the second example from the public-draft-3.0 version.
        /// </summary>
        [TestMethod]
        public void Test30ExampleTwo()
            => TestExpectedHash(
                version: "HASHBACK-PUBLIC-DRAFT-3-0",
                typeOfResponse: "BearerToken",
                issuerUrl: "https://sass.example/api/login/hashback",
                now: 1111863600,
                unus: "TmDFGekvQ+CRgANj9QPZQtBnF077gAc4AeRASFSDXo8=",
                rounds: 1,
                verifyUrl: "https://carol.example/hashback/64961859.txt",
                expectedVerificationHash: "3IoVdF2nnOJ1mwNGZYXoZcPLTsY2NyL+8JIWJB3jKzM=");

        /// <summary>
        /// Test the first example from the under-development 3.1 draft.
        /// </summary>
        [TestMethod]
        public void Test31ExampleOne()
            => TestExpectedHash(
                    version: CallerRequest.VERSION_3_1,
                    typeOfResponse: "BearerToken",
                    issuerUrl: "https://issuer.example/api/generate_bearer_token",
                    now: 529297200,
                    unus: "iZ5kWQaBRd3EaMtJpC4AS40JzfFgSepLpvPxMTAbt6w=",
                    rounds: 1,
                    verifyUrl: "https://caller.example/hashback_files/my_json_hash.txt",
                    expectedVerificationHash: "gnegmhqavAFiKctk5RTywzDKC5utN+nHjTzgNABH70Q=");

        /// <summary>
        /// Test the second example from the under-development 3.1 draft.
        /// </summary>
        [TestMethod]
        public void Test31ExampleTwo()
            => TestExpectedHash(
                version: CallerRequest.VERSION_3_1,
                typeOfResponse: "BearerToken",
                issuerUrl: "https://sass.example/api/login/hashback",
                now: 1111863600,
                unus: "TmDFGekvQ+CRgANj9QPZQtBnF077gAc4AeRASFSDXo8=",
                rounds: 1,
                verifyUrl: "https://carol.example/hashback/64961859.txt",
                expectedVerificationHash: "cMrpOXW6hMJmi9IMKEPHfvN29yfyaPEVY064coS9L8c=");
    }
}
