using System.Linq;
using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// The query/block log views append one entry per DNS lookup for as long as the app is
    /// open. Upstream issue #19 reports a 4 GB working set and #287 reports the log view being
    /// dead until restart, both of which follow from an unbounded UI-bound collection.
    /// </summary>
    public class BoundedLogCollectionTests
    {
        private const int Capacity = 3;

        [Test]
        public void AddingBeyondCapacity_TrimsToCapacity()
        {
            var collection = new BoundedObservableCollection<string>(Capacity);

            for (var i = 0; i < 10; i++)
            {
                collection.Add(i.ToString());
            }

            Assert.AreEqual(Capacity, collection.Count);
        }

        [Test]
        public void AddingBeyondCapacity_RetainsTheNewestEntries()
        {
            var collection = new BoundedObservableCollection<string>(Capacity);

            for (var i = 0; i < 10; i++)
            {
                collection.Add(i.ToString());
            }

            Assert.AreEqual(new[] { "7", "8", "9" }, collection.ToArray());
        }

        [Test]
        public void AddingWithinCapacity_KeepsEveryEntryInOrder()
        {
            var collection = new BoundedObservableCollection<string>(Capacity);

            collection.Add("a");
            collection.Add("b");
            collection.Add("c");

            Assert.AreEqual(new[] { "a", "b", "c" }, collection.ToArray());
        }
    }
}
