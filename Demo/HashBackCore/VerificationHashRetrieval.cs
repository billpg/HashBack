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

    }

    public class VerificationHashRetrieval
    {
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
            = DefaultDnsLookup;

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
        private static IPAddress DefaultDnsLookup(string host)
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

        public OnDownloadFn HashDownloadService { get; set; }
            = DefaultHashDownload;

        private static SingleVerificationHash DefaultHashDownload(IPAddress ip, Uri verifyUrl)
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

            var respAsString = Encoding.ASCII.GetString(getResponseAsBytes);

            return null;
        }

        public SingleVerificationHash RetriveHash(Uri verifyUrl)
        {
            return null;
        }
    }
}
