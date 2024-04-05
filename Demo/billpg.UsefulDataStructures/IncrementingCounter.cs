using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.UsefulDataStructures
{
    public static class IncrementingCounter
    {
        /// <summary>
        /// Start a counter and return a function that 
        /// returns a new number each time it is called
        /// in a thread-safe way.
        /// </summary>
        /// <returns>Calling delegate that returns a new integer.</returns>
        public static Func<int> Start()
        {
            /* Start a counter from zero. */
            int counter = 0;

            /* Return a function reference that
             * can see and modify the counter. */
            return IncrementInternal;
            int IncrementInternal()
            {
                /* Increment the counter in a thread-safe way. 
                 * Note that this will loop around the int limit
                 * into negative space, but the Abs call fixes that. */
                int newCounter = Interlocked.Increment(ref counter);                                
                return Math.Abs(newCounter % 5040);
            }
        }
    }
}
