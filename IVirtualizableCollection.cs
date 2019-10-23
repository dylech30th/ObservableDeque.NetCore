using System.Collections.ObjectModel;

namespace ObservableDeque.NetCore
{
    public interface IVirtualizableCollection<T>
    {
        int PageSize { get; set; }

        int Maintained { get; set; }

        int LeftIndex { get; }

        int RightIndex { get; }

        ObservableCollection<T> OuterCollection { get; set; }

        void RollDown();

        void RollUp();

        void Top();

        void End();
    }
}