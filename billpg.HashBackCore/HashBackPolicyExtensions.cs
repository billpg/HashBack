using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class HashBackPolicyExtensions
    {
        public static void RequireHost(this HashBackPolicy policy, string hostRequired)
        {
            /* Set the event handler to a simple no-case string compare. */
            policy.OnHostValidate = HostCompare;
            Task<bool> HostCompare(string hostSupplied)
            {
                /* Return if both strings are not null and are equal. */
                return Task.FromResult(
                    string.IsNullOrEmpty(hostSupplied) == false
                    && hostRequired == hostSupplied);
            }
        }

        public static void RequireAnyHost(this HashBackPolicy policy, params string[] hostsRequired)
        {
            /* Turn the array to a hashset for efficient lookups. */
            var hostsRequiredSet = new HashSet<string>(hostsRequired);

            /* Set the event handler to a is-in search. */
            policy.OnHostValidate = HostCompare;
            Task<bool> HostCompare(string hostSupplied)
            {
                /* Return if the supplied string is not null and on the list. */
                return Task.FromResult(
                    string.IsNullOrEmpty(hostSupplied) == false
                    && hostsRequiredSet.Contains(hostSupplied));
            }
        }

        public static void RequireNowWindow(this HashBackPolicy policy,
            int maxSecondsPast, int maxSecondsFuture,
            Func<long> nowGetter)
        {
            /* Set the event handler to a handler that gets the current
             * time and applies the rule. */
            policy.OnNowValidate = TimeTest;
            Task<bool> TimeTest(long nowSupplied)
            {
                /* Call the now-getter, at the time of this event. */
                long nowExpected = nowGetter();

                /* Calculate the window surrounding the current time. */
                long minAllowed = nowExpected - maxSecondsPast;
                long maxAllowed = nowExpected + maxSecondsFuture;

                /* Return if the supplied time is within the window. */
                return Task.FromResult(nowSupplied > minAllowed && nowSupplied < maxAllowed);
            }
        }

        public static void RequireNowWindow(this HashBackPolicy policy,
            int maxSecondsPast, int maxSecondsFuture,
            Func<DateTime> nowGetter)
        {
            /* Call through to the variant that takes
             * a long now with a converting wrapper. */
            policy.RequireNowWindow(
                maxSecondsPast, maxSecondsFuture,
                () => nowGetter().ToUnixTimeSeconds());
        }

        public static void RequireNowWindow(this HashBackPolicy policy,
            int maxSecondsPast, int maxSecondsFuture)
        {
            policy.RequireNowWindow(
                maxSecondsPast, maxSecondsFuture,
                Helpers.DateTimeUtcNowAsDelegate);
        }

        public static void RequireNowWindow(this HashBackPolicy policy, int maxSeconds)
            => policy.RequireNowWindow(maxSeconds, maxSeconds, Helpers.DateTimeUtcNowAsDelegate);

        public static void RequireNowWindow(this HashBackPolicy policy, int maxSeconds, Func<long> nowGetter)
            => policy.RequireNowWindow(maxSeconds, maxSeconds, nowGetter);

        public static void RequireNowWindow(this HashBackPolicy policy, int maxSeconds, Func<DateTime> nowGetter)
            => policy.RequireNowWindow(maxSeconds, maxSeconds, nowGetter);

        /// <summary>
        /// Configures the policy to reject any Unus value already seen within the supplied
        /// window, keeping an in-memory record of previously seen values. Pick a window at
        /// least as wide as the Now tolerance configured via RequireNowWindow, since a request
        /// older than that could never pass the Now check anyway.
        /// </summary>
        public static void RequireUnusNotReused(this HashBackPolicy policy, TimeSpan window)
        {
            var seenUntil = new ConcurrentDictionary<string, DateTime>();
            policy.OnUnusValidate = UnusCheck;
            Task<bool> UnusCheck(string unus)
            {
                /* Drop any entries that have aged out of the window. */
                var now = DateTime.UtcNow;
                foreach (var kvp in seenUntil)
                    if (kvp.Value < now)
                        seenUntil.TryRemove(kvp.Key, out _);

                /* Record this value, succeeding only if it wasn't already present. */
                return Task.FromResult(seenUntil.TryAdd(unus, now.Add(window)));
            }
        }
    }
}
