using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.Xml;

namespace DemoService.Services;

public interface IIpFilter
{
    bool IsAcceptable(IPAddress ip);
}

public class IpFilter : IIpFilter
{
    /// <summary>
    /// Reject any IPs in these ranges.
    /// </summary>
    private static readonly IList<IPNetwork> rejectIPs
        = new List<string>
        {
            "10.0.0.0/8",
            "127.0.0.0/8",
            "169.254.0.0/16",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "198.18.0.0/15",
            "224.0.0.0/4",
            "ff00::/8",
            "fc00::/7",
            "fe80::/10",
            "::ffff:0:0/96"
        }.Select(IPNetwork.Parse).ToList().AsReadOnly();

    /// <summary>
    /// Number of allowed GETs for this network.
    /// </summary>
    private readonly Dictionary<IPNetwork, int> quotaRemaining = new();

    // Tracks expiry times for networks. Kept in ascending expiry order.
    private readonly List<(IPNetwork net, DateTime expiry)> quotaExpiry = new();

    // Single lock used to serialize expiry cleanup + quota checks/updates.
    private readonly object quotaLock = new();

    // Overridable clock for unit tests to replace.
    public Func<DateTime> UtcNow = () => DateTime.UtcNow;

    // How long a newly-created quota lasts before being removed.
    private readonly TimeSpan quotaDuration = TimeSpan.FromHours(1);

    public bool IsAcceptable(IPAddress? ip)
    {
        /* Shortcut nulls and trivial cases. */
        if (ip is null || IPAddress.IsLoopback(ip))
            return false;

        /* Reject any weird network types. */
        if (!ip.IsIPv4() && !ip.IsIPv6())
            return false;

        /* Reject any IPs in these prohibited ranges. */
        if (rejectIPs.Any(net => net.Contains(ip)))
            return false;

        lock (quotaLock)
        {
            /* Before we check the quotas, clear any expired networks.
             * (The list is maintained in date order.) */
            int expiredCount = 0;
            for (int expiryIndex = 0; expiryIndex < quotaExpiry.Count; expiryIndex++)
            {
                if (UtcNow() > quotaExpiry[expiryIndex].expiry)
                {
                    /* This network has expired, so delete this and keep the next. */
                    expiredCount = expiryIndex+1;
                    quotaRemaining.Remove(quotaExpiry[expiryIndex].net);
                }
                else
                {
                    /* We can stop here, because the expiry records are added
                     * in order. Finding one that hasn't expired means the rest
                     * are good. */
                    break;
                }
            }
            quotaExpiry.RemoveRange(0, expiredCount);

            /* Now the expired items are cleared, so through each network the
             * supplied IP is in and see if it has exhausted its quota. */
            var nets = Helpers.Networks(ip);
            foreach (var net in nets)
            {
                /* Do we have a quota for this network? */
                if (quotaRemaining.TryGetValue(net, out var quota))
                {
                    /* If the quota has been exhausted, reject. */
                    if (quota <= 0)
                        return false;

                    /* Otherwise, deduct one quota point. */
                    quotaRemaining[net] = quota - 1;
                }

                /* First time with this network, so start its quota
                 * and its expiry, added to the end of the list. */
                else
                {
                    quotaRemaining[net] = net.NetworkQuota();
                    quotaExpiry.Add((net, UtcNow().Add(quotaDuration)));
                }
            }
        }

        /* Passed all tests. */
        return true;
    }
}