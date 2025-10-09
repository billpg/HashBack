using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class HashBackValidatorExtensions
    {
        public static void RequireHost(this HashBackValidator validator, string hostRequired)
        {
            /* Set the event handler to a simple no-case string compare. */
            validator.OnHostValidate = HostCompare;
            bool HostCompare(string hostSupplied)
            {
                /* Return if both strings are not null and are equal. */
                return
                    string.IsNullOrEmpty(hostSupplied) == false
                    && hostRequired == hostSupplied;
            }
        }

        public static void RequireAnyHost(this HashBackValidator validator, params string[] hostsRequired)
        {
            /* Turn the array to a hashset for efficient lookups. */
            var hostsRequiredSet = new HashSet<string>(hostsRequired);

            /* Set the event handler to a is-in serach. */
            validator.OnHostValidate = HostCompare;
            bool HostCompare(string hostSupplied)
            {
                /* Return if the supplied string is not null
                 * and on the list. */
                return
                    string.IsNullOrEmpty(hostSupplied) == false
                    && hostsRequiredSet.Contains(hostSupplied);
            }
        }

        public static void RequireNowWindow(this HashBackValidator validator,
            int maxSecondsPast, int maxSecondsFuture,
            Func<long> nowGetter)
        {
            /* Set the event handler to handler that gets the current
             * time and applies the rule. */
            validator.OnNowValidate = TimeTest;
            bool TimeTest(long nowSupplied)
            {
                /* Call the now-getter, at the time of this event. */
                long nowExpected = nowGetter();

                /* Calculate the window surrounding the current time. */
                long minAllowed = nowExpected - maxSecondsPast;
                long maxAllowed = nowExpected + maxSecondsFuture;

                /* Return if the supplied time is within the window. */
                return nowSupplied > minAllowed
                    && nowSupplied < maxAllowed;
            }
        }

        public static void RequireNowWindow(this HashBackValidator validator,
            int maxSecondsPast, int maxSecondsFuture,
            Func<DateTime> nowGetter)
        {
            /* Call through to the varient that takes
             * a long now with a converting wrapper. */
            validator.RequireNowWindow(
                maxSecondsPast, maxSecondsFuture,
                () => nowGetter().ToUnixTimeSeconds());
        }

        public static void RequireNowWindow(this HashBackValidator validator,
            int maxSecondsPast, int maxSecondsFuture)
        {
            validator.RequireNowWindow(
                maxSecondsPast, maxSecondsFuture,
                Helpers.DateTimeUtcNowAsDelegate);
        }

        public static void RequireNowWindow(this HashBackValidator validator, int maxSeconds)
            => validator.RequireNowWindow(maxSeconds, maxSeconds, Helpers.DateTimeUtcNowAsDelegate);

        public static void RequireNowWindow(this HashBackValidator validator, int maxSeconds, Func<long> nowGetter)
            => validator.RequireNowWindow(maxSeconds, maxSeconds, nowGetter);

        public static void RequireNowWindow(this HashBackValidator validator, int maxSeconds, Func<DateTime> nowGetter)
            => validator.RequireNowWindow(maxSeconds, maxSeconds, nowGetter);


    }
}
