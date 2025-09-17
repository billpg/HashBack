using System;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HashBackCoreTests
{
    [TestClass]
    public sealed class SaltedHashTests
    {
        [TestMethod]
        public void ComputeSaltedHash_KnownExample_MatchesReadme()
        {
            /* Example from README.md (BASE64 block for the example request) */
            string base64 = 
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJzZXJ2ZXIuZXhh" +
                "bXBsZSIsIk5vdyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3Bz" +
                "L2hZUT09IiwiVmVyaWZ5IjoiaHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFz" +
                "aGJhY2s/aWQ9NTAyNTQyODg2In0=";

            /* Run the hash function and compare to expected value */
            Assert.AreEqual(
                "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=",
                Helpers.ComputeVerificationHash(base64), 
                "Hash does not match README example.");
        }

        [TestMethod]
        public void ComputeSaltedHash_EmptyInput_ValidHash()
        {
            Assert.AreEqual(
                "eeaew/uoMm/i4w/l1WYRsIzCFI2rKQb44QopJqDcwgk=", 
                Helpers.ComputeVerificationHash(""), 
                "An empty string does not hash to the expected result.");
        }

        [TestMethod]
        public void ComputeSaltedHash_SameInput_SameHash()
        {
            byte[] input = Encoding.UTF8.GetBytes("test input");
            string hash1 = Helpers.ComputeVerificationHash(input);
            string hash2 = Helpers.ComputeVerificationHash(input);

            Assert.AreEqual(hash1, hash2, "Hashes for same input should match.");
        }

        [TestMethod]
        public void ComputeSaltedHash_DifferentInput_DifferentHash()
        {
            byte[] input1 = Encoding.UTF8.GetBytes("input one");
            byte[] input2 = Encoding.UTF8.GetBytes("input two");

            string hash1 = Helpers.ComputeVerificationHash(input1);
            string hash2 = Helpers.ComputeVerificationHash(input2);

            Assert.AreNotEqual(hash1, hash2, "Hashes for different input should not match.");
        }


    public void a()
        {
            /* Open a builder object that's configured to use a hash registry. */
            var builder = new billpg.HashBackCore.AuthHeaderBuilder()
                .WithHashRegistry(
                    baseUrl: "https://mywebsite.example/api/hashback",
                    queryParamName: "id",
                    register: RegisterHash);

            /* Hash registry. The above object will call this function
             * when it has generated a verification hash. */
            void RegisterHash(Guid id, string hash)
            {
                /* In a real application, you would store the hash in a 
                 * database, upload it as a text file to your website's
                 * SFTP folder, or otherwise save it for later retrieval.
                 * This example only prints it to the console. */
                Console.WriteLine($"Register hash for id {id}: {hash}");
            }

            /* Call the builder to create an Authorization header. */

        }
    }    }

