using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableDeque.NetCore
{
    public class ConcurrentObservableDeque<T> : ConcurrentDeque<T>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public ConcurrentObservableDeque()
        {

        }

        public ConcurrentObservableDeque(IEnumerable<T> collection) : base(collection)
        {
        }

        public override void PushRight(T item)
        {
            base.PushRight(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, Count - 1));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        public override void PushLeft(T item)
        {
            base.PushLeft(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, 0));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        public override bool TryPopRight(out T item)
        {
            var result = base.TryPopRight(out item);
            if (result)
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, Count));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }

            return result;
        }

        public override bool TryPopLeft(out T item)
        {
            var result = base.TryPopLeft(out item);
            if (result)
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, 0));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }

            return result;
        }

        public override void Clear()
        {
            base.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }
}