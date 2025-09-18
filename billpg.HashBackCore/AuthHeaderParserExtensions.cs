using System;

namespace billpg.HashBackCore
{
    public static class AuthHeaderParserExtensions
    {
        /// <summary>
        /// Return a new parser with a custom time source and allowing
        /// a request to have a presented time within the specified tolerance.
        /// </summary>
        /// <param name="parser">Parser object to wrap.</param>
        /// <param name="nowGetter">Custom now-getter callable.</param>
        /// <param name="secondsTolerance">Number of seconds tolerance.</param>
        /// <returns>New parser object with modified parser.</returns>
        public static AuthHeaderValidator WithTimeTolerance(
            this AuthHeaderValidator parser,
            Func<long> nowGetter,
            long secondsTolerance)
        {
            /* Wrap the provided now-getter and
             * tolerance into a NowTest function. */
            return parser.WithNowTest(Internal);

            /* The actual NowTest function that checks
             * if the presented time is within tolerance. */
            bool Internal(long nowPresented)
            {
                /* Call the now-getter, at the time of the check. */
                long nowExpected = nowGetter();

                /* Check if presented time is within the tolerance window. */
                long diff = Math.Abs(nowPresented - nowExpected);
                return diff < secondsTolerance;
            }
        }


        public static AuthHeaderValidator WithTimeTolerance(
            this AuthHeaderValidator parser,
            Func<DateTime> nowGetter,
            long secondsTolerance)
        {
            /* Call through to the Func<long> varient. */
            return WithTimeTolerance(parser, WrapNowGetter, secondsTolerance);

            /* Wrap the now-getter functon to convert the result. */
            long WrapNowGetter()
                => nowGetter().ToUnixTimeSeconds();
        }

        /// <summary>
        /// Returns a new AuthHeaderValidator with a time tolerance using 
        /// DateTime.UtcNow (at the time the parser is called) as the expected time.
        /// </summary>
        /// <param name="parser">The parser to extend.</param>
        /// <param name="seconds">The allowed tolerance in secondsTolerance.</param>
        /// <returns>A new AuthorizationPolicy with the time tolerance applied.</returns>
        public static AuthHeaderValidator WithTimeTolerance(
            this AuthHeaderValidator parser,
            long seconds)
        {
            /* Call through to the other WithTimeTolerance function, 
             * passing in DateTime.UtcNow as the time source. */
            return parser.WithTimeTolerance(DateTimeUtcNowAsDelegate, seconds);
        }

        /// <summary>
        /// DateTime.UtcNow wrapped in a function, 
        /// so it can be passed as a delegate.
        /// </summary>
        /// <returns>Result of calling DateTime.UtcNow 
        /// at the point it is called.</returns>
        private static DateTime DateTimeUtcNowAsDelegate()
            => DateTime.UtcNow;

        /// <summary>
        /// Requires that the Host property in the
        /// Authorization header has the specified value.
        /// </summary>
        /// <param name="parser">Parser object to wrap.</param>
        /// <param name="hostRequired">A single host name to require.</param>
        /// <returns>New parser object with the supplied host parser.</returns>
        public static AuthHeaderValidator WithRequiredHost(
            this AuthHeaderValidator parser,
            string hostRequired)
        {
            /* Return new parser with a HostTest 
             * that checks for exact match. */
            return parser.WithHostTest(Internal);
            bool Internal(string hostPresented)
                => hostPresented == hostRequired;
        }

        /// <summary>
        /// Returns a new parser that requires the Host property
        /// is one of the supplied values.
        /// </summary>
        /// <param name="parser">Parser object to wrap.</param>
        /// <param name="hostAnyRequired">List of allowed host strings.</param>
        /// <returns>New parser object that wraps the old parser.</returns>
        public static AuthHeaderValidator WithAnyRequiredHost(
            this AuthHeaderValidator parser,
            params string[] hostAnyRequired)
        {
            /* Convert the array to a hash-set for faster lookups. */
            var hostAnyRequiredSet = new HashSet<string>(hostAnyRequired);

            /* Return new parser with a HostTest 
             * that checks for exact match of any 
             * of the supplied host names. */
            return parser.WithHostTest(Internal);
            bool Internal(string hostPresented)
                => hostAnyRequiredSet.Contains(hostPresented);
        }
    }
}