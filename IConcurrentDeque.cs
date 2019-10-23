using System.Collections.Concurrent;

namespace ObservableDeque.NetCore
{
    public interface IConcurrentDeque<T> : IProducerConsumerCollection<T>
    {
        bool IsEmpty { get; }

        void PushRight(T item);

        void PushLeft(T item);

        bool TryPopRight(out T item);

        bool TryPopLeft(out T item);

        bool TryPeekRight(out T item);

        bool TryPeekLeft(out T item);

        void Clear();
    }
}