using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ObservableDeque.NetCore
{
    public class ConcurrentDeque<T> : IConcurrentDeque<T>
    {
        internal volatile Anchor MainAnchor;

        public bool IsEmpty => MainAnchor.Left == null;

        public int Count => ToList().Count;

        public ConcurrentDeque()
        {
            MainAnchor = new Anchor();
        }

        public ConcurrentDeque(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            InitializeFromCollection(collection);
        }

        private void InitializeFromCollection(IEnumerable<T> collection)
        {
            var iterator = collection.GetEnumerator();
            if (iterator.MoveNext())
            {
                var first = new Node(iterator.Current);
                var last = first;

                while (iterator.MoveNext())
                {
                    var newLast = new Node(iterator.Current)
                    {
                        Left = last
                    };
                    last.Right = newLast;

                    last = newLast;
                }

                MainAnchor = new Anchor(first, last, DequeStatus.Stable);
            }
            else
            {
                MainAnchor = new Anchor();
            }
            iterator.Dispose();
        }

        public virtual void PushRight(T item)
        {
            var newNode = new Node(item);
            var spinner = new SpinWait();

            while (true)
            {
                var anchor = MainAnchor;
                anchor.Validate();

                if (anchor.Right == null)
                {
                    var newAnchor = new Anchor(newNode, newNode, anchor.Status);

                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        return;
                }
                else if (anchor.Status == DequeStatus.Stable)
                {
                    newNode.Left = anchor.Right;

                    var newAnchor = new Anchor(anchor.Left, newNode, DequeStatus.RPush);

                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                    {
                        StabilizeRight(newAnchor);
                        return;
                    }
                }
                else
                {
                    Stabilize(anchor);
                }

                spinner.SpinOnce();
            }
        }

        public virtual void PushLeft(T item)
        {
            var newNode = new Node(item);
            var spinner = new SpinWait();

            while (true)
            {
                var anchor = MainAnchor;
                anchor.Validate();

                if (anchor.Left == null)
                {
                    var newAnchor = new Anchor(newNode, newNode, anchor.Status);

                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        return;
                }
                else if (anchor.Status == DequeStatus.Stable)
                {
                    newNode.Right = anchor.Left;

                    var newAnchor = new Anchor(newNode, anchor.Right, DequeStatus.LPush);

                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                    {
                        StabilizeLeft(newAnchor);
                        return;
                    }
                }
                else
                {
                    Stabilize(anchor);
                }

                spinner.SpinOnce();
            }
        }

        public virtual bool TryPopRight(out T item)
        {
            Anchor anchor;
            var spinner = new SpinWait();

            while (true)
            {
                anchor = MainAnchor;
                anchor.Validate();

                if (anchor.Right == null)
                {
                    //return false if the deque is empty
                    item = default;
                    return false;
                }
                if (anchor.Right == anchor.Left)
                {
                    //update both pointers if the deque has only one node
                    var newAnchor = new Anchor();
                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        break;
                }
                else if (anchor.Status == DequeStatus.Stable)
                {
                    //update right pointer if deque has > 1 node
                    var prev = anchor.Right.Left;
                    var newAnchor = new Anchor(anchor.Left, prev, anchor.Status);
                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        break;
                }
                else
                {
                    //if the deque is unstable,
                    //attempt to bring it to a stable state before trying to remove the node.
                    Stabilize(anchor);
                }

                spinner.SpinOnce();
            }

            var node = anchor.Right;
            item = node.Value;

            /*
             * Try to set the new rightmost node's right pointer to null to avoid memory leaks.
             * We try only once - if CAS fails, then another thread must have pushed a new node, in which case we simply carry on.
             */
            var rightmostNode = node.Left;
            if (rightmostNode != null)
                Interlocked.CompareExchange(ref rightmostNode.Right, null, node);

            return true;
        }

        public virtual bool TryPopLeft(out T item)
        {
            Anchor anchor;
            var spinner = new SpinWait();

            while (true)
            {
                anchor = MainAnchor;
                anchor.Validate();

                if (anchor.Left == null)
                {
                    //return false if the deque is empty
                    item = default;
                    return false;
                }
                if (anchor.Right == anchor.Left)
                {
                    //update both pointers if the deque has only one node
                    var newAnchor = new Anchor();
                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        break;
                }
                else if (anchor.Status == DequeStatus.Stable)
                {
                    //update left pointer if deque has > 1 node
                    var prev = anchor.Left.Right;
                    var newAnchor = new Anchor(prev, anchor.Right, anchor.Status);
                    if (Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor) == anchor)
                        break;
                }
                else
                {
                    //if the deque is unstable,
                    //attempt to bring it to a stable state before trying to remove the node.
                    Stabilize(anchor);
                }

                spinner.SpinOnce();
            }

            var node = anchor.Left;
            item = node.Value;

            /*
             * Try to set the new leftmost node's left pointer to null to avoid memory leaks.
             * We try only once - if CAS fails, then another thread must have pushed a new node, in which case we simply carry on.
             */
            var leftmostNode = node.Right;
            if (leftmostNode != null)
                Interlocked.CompareExchange(ref leftmostNode.Left, null, node);

            return true;
        }

        public virtual bool TryPeekRight(out T item)
        {
            var rightmostNode = MainAnchor.Right;

            if (rightmostNode != null)
            {
                item = rightmostNode.Value;
                return true;
            }
            item = default;
            return false;
        }

        public virtual bool TryPeekLeft(out T item)
        {
            var leftmostNode = MainAnchor.Left;

            if (leftmostNode != null)
            {
                item = leftmostNode.Value;
                return true;
            }
            item = default;
            return false;
        }

        public virtual void Clear()
        {
            MainAnchor = new Anchor();
        }

        private void Stabilize(Anchor anchor)
        {
            if (anchor.Status == DequeStatus.RPush)
                StabilizeRight(anchor);
            else
                StabilizeLeft(anchor);
        }

        private void StabilizeRight(Anchor anchor)
        {
            if (MainAnchor != anchor)
                return;

            var newNode = anchor.Right;
            var prev = newNode.Left;

            if (prev == null)
                return;

            var prevNext = prev.Right;
            if (prevNext != newNode)
            {
                if (MainAnchor != anchor)
                    return;

                if (Interlocked.CompareExchange(ref prev.Right, newNode, prevNext) != prevNext)
                    return;
            }

            var newAnchor = new Anchor(anchor.Left, anchor.Right, DequeStatus.Stable);
            Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor);
        }

        private void StabilizeLeft(Anchor anchor)
        {
            if (MainAnchor != anchor)
                return;

            var newNode = anchor.Left;
            var prev = newNode.Right;

            if (prev == null)
                return;

            var prevNext = prev.Left;
            if (prevNext != newNode)
            {
                if (MainAnchor != anchor)
                    return;
                if (Interlocked.CompareExchange(ref prev.Left, newNode, prevNext) != prevNext)
                    return;
            }

            var newAnchor = new Anchor(anchor.Left, anchor.Right, DequeStatus.Stable);
            Interlocked.CompareExchange(ref MainAnchor, newAnchor, anchor);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        object ICollection.SyncRoot => throw new NotSupportedException("The SyncRoot property may not be used for the synchronization of concurrent collections.");

        bool ICollection.IsSynchronized => false;

        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            PushRight(item);
            return true;
        }

        bool IProducerConsumerCollection<T>.TryTake(out T item)
        {
            return TryPopLeft(out item);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            ((ICollection)ToList()).CopyTo(array, index);
        }

        public void CopyTo(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            ToList().CopyTo(array, index);
        }

        public T[] ToArray()
        {
            return ToList().ToArray();
        }

        private List<T> ToList()
        {
            var anchor = MainAnchor;
            anchor.Validate();

            if (anchor.Status != DequeStatus.Stable)
            {
                var spinner = new SpinWait();
                do
                {
                    anchor = MainAnchor;
                    anchor.Validate();

                    spinner.SpinOnce();
                } while (anchor.Status != DequeStatus.Stable);
            }

            var x = anchor.Left;
            var y = anchor.Right;

            if (x == null)
                return new List<T>();

            if (x == y)
                return new List<T> { x.Value };

            var xaPath = new List<Node>();
            var current = x;
            while (current != null && current != y)
            {
                xaPath.Add(current);
                current = current.Right;
            }

            if (current == y)
            {
                xaPath.Add(current);
                return xaPath.Select(node => node.Value).ToList();
            }

            current = y;
            var a = xaPath.Last();
            var ycPath = new Stack<Node>();
            while (current.Left != null &&
                   current.Left.Right != current &&
                   current != a)
            {
                ycPath.Push(current);
                current = current.Left;
            }
            var common = current;
            ycPath.Push(common);

            var xySequence = xaPath
                .TakeWhile(node => node != common)
                .Select(node => node.Value)
                .Concat(
                    ycPath.Select(node => node.Value));

            return xySequence.ToList();
        }

        internal class Anchor
        {
            internal readonly Node Left;
            internal readonly Node Right;
            internal readonly DequeStatus Status;

            internal Anchor()
            {
                Right = Left = null;
                Status = DequeStatus.Stable;
            }

            internal Anchor(Node left, Node right, DequeStatus status)
            {
                Left = left;
                Right = right;
                Status = status;
            }

            [Conditional("DEBUG")]
            public void Validate()
            {
                Debug.Assert((Left == null && Right == null) ||
                                (Left != null && Right != null));

                if (Left == null)
                    Debug.Assert(Status == DequeStatus.Stable);
            }
        }

        internal enum DequeStatus
        {
            Stable,
            LPush,
            RPush
        };

        internal class Node
        {
            internal volatile Node Left;
            public volatile Node Right;
            internal readonly T Value;

            internal Node(T value)
            {
                Value = value;
            }
        }
    }
}