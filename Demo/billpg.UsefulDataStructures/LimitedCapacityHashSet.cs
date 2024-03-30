using System.Data;
using System.IO.Pipes;

namespace billpg.UsefulDataStructures
{
    public class LimitedCapacityHashSet<T> where T : notnull
    {
        public int MaxCapacity { get; }
        private readonly HashSet<T> data = new HashSet<T>();
        private readonly Queue<T> toDelete = new Queue<T>();

        public LimitedCapacityHashSet(int maxCapacity)
        {
            this.MaxCapacity = maxCapacity;
        }

        public void Add(T value)
        {
            /* Add to the hash set. It may or may not already be present. */
            data.Add(value);
            toDelete.Enqueue(value);

            /* Keep looping until we're back below capacity. */
            while (data.Count > MaxCapacity)
            {
                /* Select the longest waiting and delete it. */
                T valueToDelete = toDelete.Dequeue();
                data.Remove(valueToDelete);
            }
        }

        public bool Contains(T value) => data.Contains(value);
    }
}
