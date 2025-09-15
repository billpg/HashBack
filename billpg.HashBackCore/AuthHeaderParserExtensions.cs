using System;

namespace billpg.HashBackCore
{
    public static class AuthorizationPolicyExtensions
    {
        public static AuthHeaderParser WithTimeTolerance(
            this AuthHeaderParser policy,
            Func<DateTime> nowGetter,
            long secondsTolerance)
        {
            /* Wrap the provided now-getter and
             * tolerance into a NowTest function. */
            return policy.WithNowTest(Internal);

            /* The actual NowTest function that checks
             * if the presented time is within tolerance. */
            bool Internal(long nowPresented)
            {
                /* Call the now-getter, at the time of the check. */
                long nowExpected = nowGetter().ToUnixTimeSeconds();

                /* Check if presented time is within the tolerance window. */
                long diff = Math.Abs(nowPresented - nowExpected);
                return diff < secondsTolerance;
            }
        }

        /// <summary>
        /// Returns a new AuthorizationPolicy with a time tolerance using 
        /// DateTime.UtcNow (at the time the parser is called) as the expected time.
        /// </summary>
        /// <param name="policy">The policy to extend.</param>
        /// <param name="seconds">The allowed tolerance in secondsTolerance.</param>
        /// <returns>A new AuthorizationPolicy with the time tolerance applied.</returns>
        public static AuthHeaderParser WithTimeTolerance(
            this AuthHeaderParser policy,
            long seconds)
        {
            /* Call through to the other WithTimeTolerance function, 
             * passing in DateTime.UtcNow as the time source. */
            return policy.WithTimeTolerance(DateTimeUtcNowAsDelegate, seconds);
        }

        /// <summary>
        /// DateTime.UtcNow wrapped in a function, 
        /// so it can be passed as a delegate.
        /// </summary>
        /// <returns>Result of calling DateTime.UtcNow 
        /// at the point it is called.</returns>
        private static DateTime DateTimeUtcNowAsDelegate()
            => DateTime.UtcNow;

        public static AuthHeaderParser WithRequireHostName(
            this AuthHeaderParser policy,
            string requiredHostName)
        {
            return policy.WithHostTest(host => Helpers.EqualsNoCase(host, requiredHostName));
        }

        public static AuthHeaderParser WithAnyRequiredHostName(
            this AuthHeaderParser policy,
            params string[] requiredHostNames)
        {
            return policy.WithHostTest(Internal);
            bool Internal(string hostPresented)
            {
                /* Check against all required host names. */
                foreach (var requiredHost in requiredHostNames)
                {
                    /* If any match, return true. */
                    if (Helpers.EqualsNoCase(hostPresented, requiredHost))
                        return true;
                }
                /* If we got here, none matched. */
                return false;
            }
        }
    }
}