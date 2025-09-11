using System;

namespace billpg.HashBackCore
{
    public static class AuthorizationPolicyExtensions
    {
        public static AuthorizationPolicy WithTimeTolerance(
            this AuthorizationPolicy policy,
            Func<DateTime> nowGetter,
            long seconds)
        {
            return policy.WithNowTest(now =>
            {
                long current = nowGetter().ToUnixTimeSeconds();
                return (now >= current - seconds) && (now <= current + seconds);
            });
        }

        /// <summary>
        /// Returns a new AuthorizationPolicy with a time tolerance using 
        /// DateTime.UtcNow (at the time the parser is called) as the current time.
        /// </summary>
        /// <param name="policy">The policy to extend.</param>
        /// <param name="seconds">The allowed tolerance in seconds.</param>
        /// <returns>A new AuthorizationPolicy with the time tolerance applied.</returns>
        public static AuthorizationPolicy WithTimeTolerance(
            this AuthorizationPolicy policy,
            long seconds)
        {
            return policy.WithTimeTolerance(() => DateTime.UtcNow, seconds);
        }

        public static AuthorizationPolicy WithRequireHostName(
            this AuthorizationPolicy policy,
            string requiredHostName)
        {
            return policy.WithHostTest(host => Helpers.EqualsNoCase(host, requiredHostName));
        }
    }
}