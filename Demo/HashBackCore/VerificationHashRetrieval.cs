using billpg.UsefulDataStructures;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class SingleVerificationHash
    {
        private IPAddress ip;
        private Uri verifyUrl;
        private X509Certificate2? certForTls;
        private IList<byte> hash;

        public SingleVerificationHash(IPAddress ip, Uri verifyUrl, X509Certificate2? certForTls, IList<byte> hash)
        {
            this.ip = ip;
            this.verifyUrl = verifyUrl;
            this.certForTls = certForTls;
            this.hash = hash;
        }
    }

    public class VerificationHashRetrieval
    {
        public VerificationHashRetrieval()
        {
            this.OnRetrieveError = msg => throw new NotImplementedException();
            this.NameLookupService = this.DefaultDnsLookup;
            this.HashDownloadService = this.DefaultHashDownload;
        }

        public delegate Exception OnRetrieveErrorFn(string message);
        public OnRetrieveErrorFn OnRetrieveError { get; set; }

        /// <summary>
        /// Functon signature for the DNS lookup function.
        /// </summary>
        /// <param name="host">Name to be looked up.</param>
        /// <returns>A single IP address or IPAddress.None if no-such-name.</returns>
        public delegate IPAddress OnNameLookupFn(string host);

        /// <summary>
        /// Current function to preform DNS lookups. Defaults to actual
        /// DNS lookup but may be replaced by unit-tests.
        /// </summary>
        public OnNameLookupFn NameLookupService { get; set; }

        /// <summary>
        /// A counter to assist making choices when there are many
        /// alternatices. Will be incremented 
        /// </summary>
        private static readonly Func<int> multipleOptionSelector 
            = IncrementingCounter.Start();

        /// <summary>
        /// The default NameLookup provider that actually calls DNS.
        /// </summary>
        /// <param name="host">Name to lokup, may use IDN.</param>
        /// <returns>IPAddress for this host or IPAddress.None.</returns>
        private IPAddress DefaultDnsLookup(string host)
        {
            /* Perform the DNS lookup. Note that this function does
             * the right thing with IDN domains. */
            IPAddress[] addrs;
            try
            {
                addrs = Dns.GetHostAddresses(host);
            }
            /* Catch only NXDOMAIN erors and return "None" for this error only. */
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
            {
                return IPAddress.None;
            }

            /* Handle simple zero and one cases. */
            if (addrs.Length == 0) return IPAddress.None;
            if (addrs.Length == 1) return addrs[0];

            /* If multiple addresses, pick one based on the rotating counter. */
            return addrs[multipleOptionSelector() % addrs.Length];
        }

        public delegate SingleVerificationHash OnDownloadFn(IPAddress ip, Uri verifyUrl);

        public OnDownloadFn HashDownloadService { get; set; } = null!;

        private SingleVerificationHash DefaultHashDownload(IPAddress ip, Uri verifyUrl)
        {
            /* Build a request package. This is suitable for a one-shot request that will
             * return the reuqetsed hash and close the connection. */
            var getRequestAsString =
                $"GET {verifyUrl.PathAndQuery} HTTP/1.1" + "\r\n" +
                $"Host: {verifyUrl.Host}" + "\r\n" +
                "Connection: close" + "\r\n" +
                "Accept-Encoding: identity" + "\r\n" +
                "Accept: text/plain" + "\r\n" +
                "User-Agent: HashBack Demo VerifyHash download." +
                " Please report issues to https://github.com/billpg/HashBack/issues" + "\r\n" +
                 "\r\n";

            /* Function to receive the TLS certificate when handshaken. */
            X509Certificate2? certForTls = null;

            /* Open a TCP socket and send. */
            var getRequestAsBytes = Encoding.ASCII.GetBytes(getRequestAsString);
            var getResponseAsBytes = SpartanHttp.Run(
                ip,
                verifyUrl.Scheme.ToLowerInvariant() == "https", 
                verifyUrl.Port,
                verifyUrl.Host,
                cert => certForTls = cert,
                getRequestAsBytes);

            /* Parse the response to extract the pertinent data.
             * (May throw exception if response not valid.) */
            IList<byte> hash = ValidateResponseExtractVerificationHash(
                verifyUrl.Host, OnRetrieveError, getResponseAsBytes);

            /* Return to caller with collected data. */
            return new SingleVerificationHash(ip, verifyUrl, certForTls, hash);
        }

        private static IList<byte> ValidateResponseExtractVerificationHash(
            string urlHost, OnRetrieveErrorFn onRetrieveError, byte[] response)
        {
            /* Convert the block of bytes into lines. */
            using var ms = new MemoryStream(response);
            using var sr = new StreamReader(ms, Encoding.UTF8);
            var lines = new List<string>();
            while (true)
            {
                string? line = sr.ReadLine();
                if (line == null)
                    break;
                lines.Add(line);
            }

            /* If response is empty, complain. */
            if (lines.Count < 4)
                throw onRetrieveError($"HTTP response from {urlHost} has insufficient line count.");

            /* The first line should have two parts, of which we're
             * interested in the second only. It may have more. */
            var firstLineBySpace = lines[0].Split(' ').ToList();
            if (firstLineBySpace.Count < 2)
                throw onRetrieveError($"First line of HTTP response from {urlHost} was not a valid response prologue.");
            string statusCode = firstLineBySpace[1];
            if (statusCode != "200")
            {
                if (int.TryParse(statusCode, out int statusCodeAsInt) && statusCodeAsInt >= 100 && statusCodeAsInt <= 499)
                    throw onRetrieveError($"Expected HTTP status code 200 from {urlHost}, got {statusCode}.");
                else
                    throw onRetrieveError($"No HTTP status code from {urlHost}.");
            }

            /* Look for the blank line between header and body. */
            int blankLineIndex = lines.IndexOf("");
            if (blankLineIndex < 0)
                throw onRetrieveError($"HTTP response from {urlHost} did not include an empty line.");

            /* Separate the header lines. */
            var headers = lines.Skip(1).Take(blankLineIndex - 1).ToList();

            /* Loop through the headers, looking for split lines. */
            while (true)
            {
                /* Look for a single split line. Line zero can't be a split line. If none, stop. */
                int splitLineIndex = headers.FindIndex(1, line => char.IsWhiteSpace(line[0]));
                if (splitLineIndex < 0)
                    break;

                /* Join the two lines and close gap. */
                headers[splitLineIndex - 1] += headers[splitLineIndex];
                headers.RemoveAt(splitLineIndex);
            }

            /* Look for the Content-Type header. */
            int contentTypeIndex = headers.FindIndex(line => line.ToLowerInvariant().StartsWith("content-type:"));
            if (contentTypeIndex < 0)
                throw onRetrieveError($"HTTP reponse from {urlHost} did not include a Content-Type header.");
            string contentType = headers[contentTypeIndex];

            /* Pull out the type, for now ignoring the charset. */
            var contentTypeByColon = contentType.Split(':', ';').Select(x => x.Trim()).ToList();
            if (contentTypeByColon.Count < 2 || contentTypeByColon[1].ToLowerInvariant() != "text/plain")
                throw onRetrieveError($"HTTP response from {urlHost} must include \"Content-Type: text/plain\" header.");

            /* Loop through all the response body lines, looking for a valid hash. */
            var hashAsBytes = 
                lines
                .Skip(blankLineIndex+1)
                .Select(line => HashService.ConvertFromBase64OrNull(line.Trim(), 32))
                .FirstOrDefault(hashBytes => hashBytes != null);
            if (hashAsBytes == null)
                throw onRetrieveError(
                    $"HTTP response body from {urlHost} must contain a" +
                    " line with 256-bit hash, BASE-64 encoded.");

            /* Finally, found a 256 bit hash. Return as success. */
            return hashAsBytes;
        }

        public SingleVerificationHash RetriveHash(Uri verifyUrl)
        {
            return null;
        }
    }
}
