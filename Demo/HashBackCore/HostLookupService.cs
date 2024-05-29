using billpg.UsefulDataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class HostLookupService
    {
        /// <summary>
        /// A counter to assist making choices when there are many
        /// alternatices. Will be incremented each time it is called.
        /// </summary>
        private static readonly IncrementingCounter.SelectFn multipleOptionSelector
            = IncrementingCounter.Start();

        /// <summary>
        /// Lookup function for this service object. Delegate returned is suitable for
        /// performing DNS lookups.
        /// </summary>
        public OnHostLookupFn Lookup
            => LookupInternal;

        /// <summary>
        /// Perform a simple lookup and return the IPAddress for the supplied host name.
        /// </summary>
        /// <param name="host">Name to lookup.</param>
        /// <returns>Dscovered IP, or IPAdress.None if not dfound.</returns>
        private IPAddress LookupInternal(string host)
        {
            /* Perform the DNS lookup. Note that this function does the right thing with 
             * IDN domains. Also note that the OS will cache responses so we don't have to. */
            List<IPAddress> addrs;
            try
            {
                addrs = Dns.GetHostAddresses(host).ToList();
            }
            /* Catch only NXDOMAIN responses and return "None" for this error only. */
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
            {
                return IPAddress.None;
            }

            /* If there are a mixture of IPv4 and IPv6, delete the IPv4s. */
            static bool IsIPv4(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetwork;
            static bool IsIPv6(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetworkV6;
            if (addrs.Any(IsIPv4) && addrs.Any(IsIPv6))
                addrs.RemoveAll(IsIPv4);

            /* Handle simple zero and one cases. */
            if (addrs.Count == 0) return IPAddress.None;
            if (addrs.Count == 1) return addrs[0];

            /* If multiple addresses, pick one based on the rotating counter. */
            return addrs[multipleOptionSelector(addrs.Count)];
        }
    }
}
