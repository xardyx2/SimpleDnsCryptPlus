using System.Collections.ObjectModel;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that discards the oldest entries once it is
    /// full, so a UI-bound log viewer cannot grow without limit.
    /// </summary>
    public class BoundedObservableCollection<T> : ObservableCollection<T>
    {
        private readonly int _capacity;

        public BoundedObservableCollection(int capacity)
        {
            _capacity = capacity;
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);

            while (Count > _capacity)
            {
                RemoveItem(0);
            }
        }
    }
}
