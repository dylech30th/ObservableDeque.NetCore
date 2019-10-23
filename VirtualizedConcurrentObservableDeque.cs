using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ObservableDeque.NetCore
{
    public class VirtualizedConcurrentObservableDeque<T> : ConcurrentObservableDeque<T>, IVirtualizableCollection<T>
    {
        public int PageSize { get; set; }

        public int Maintained { get; set; }

        public int LeftIndex { get; private set; }

        public int RightIndex { get; private set; }

        public ObservableCollection<T> OuterCollection { get; set; }

        public VirtualizedConcurrentObservableDeque(int pageSize, int maintained, ObservableCollection<T> outerCollection)
        {
            PageSize = pageSize;
            Maintained = maintained;
            CheckPageSize();
            OuterCollection = outerCollection;

            if (OuterCollection.Count < Maintained)
            {
                OuterCollection.CollectionChanged += OuterCollection_CollectionChanged;
            }
            else
            {
                InitContainer();
            }
        }

        private void OuterCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (OuterCollection.Count >= Maintained)
            {
                OuterCollection.CollectionChanged -= OuterCollection_CollectionChanged;
            }

            LeftIndex = 0;
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var argsNewItem in e.NewItems)
                {
                    PushRight((T)argsNewItem);
                    RightIndex++;
                }
            }
        }


        private void InitContainer()
        {
            Clear();
            LeftIndex = 0;
            RightIndex = 0;

            for (var i = 0; i < (OuterCollection.Count < Maintained ? OuterCollection.Count : Maintained); i++, RightIndex++)
            {
                PushRight(OuterCollection[i]);
            }

            RightIndex--;
        }

        private void CheckPageSize()
        {
            if (PageSize > Maintained)
            {
                throw new ArgumentOutOfRangeException(nameof(PageSize), $"mustn't bigger than {nameof(Maintained)}: {Maintained}");
            }
        }

        public void RollDown()
        {
            var outerLastIndex = OuterCollection.Count - 1;
            var right = RightIndex;
            if (right != outerLastIndex)
            {
                if (outerLastIndex - right < PageSize)
                {
                    for (var i = right + 1; i <= outerLastIndex; i++, LeftIndex++, RightIndex++)
                    {
                        if (TryPopLeft(out _))
                        {
                            PushRight(OuterCollection[i]);
                        }
                    }
                }
                else
                {
                    for (var i = right + 1; i <= right + PageSize; i++, LeftIndex++, RightIndex++)
                    {
                        if (TryPopLeft(out _))
                        {
                            PushRight(OuterCollection[i]);
                        }
                    }
                }
            }
        }

        public void RollUp()
        {
            var left = LeftIndex;
            if (left != 0)
            {
                // if remain elements before OuterCollection[LeftIndex] is less than PageSize, that means there's only LeftIndex + 1 elements before OuterCollection[LeftIndex]
                if (left < PageSize)
                {
                    for (var i = left - 1; i >= 0; i--, LeftIndex--, RightIndex--)
                    {
                        if (TryPopRight(out _))
                        {
                            PushLeft(OuterCollection[i]);
                        }
                    }
                }
                else
                {
                    var targetLeft = left - PageSize;
                    for (var i = left - 1; i >= targetLeft; i--, LeftIndex--, RightIndex--)
                    {
                        if (TryPopRight(out _))
                        {
                            PushLeft(OuterCollection[i]);
                        }
                    }
                }
            }
        }

        public void Top()
        {
            if (OuterCollection.Count >= Maintained)
            {
                InitContainer();
            }
        }

        public void End()
        {
            var count = OuterCollection.Count;
            if (count >= Maintained)
            {
                Clear();

                for (var i = count - Maintained; i < count; i++)
                {
                    PushRight(OuterCollection[i - 1]);
                }

                RightIndex = count - 1;
                LeftIndex = RightIndex - Maintained;
            }
        }
    }
}