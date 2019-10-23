using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ObservableDeque.NetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var obCollection = new ObservableCollection<int>();
            FillCollections(obCollection);

            const int pageSize = 20;
            const int maintained = 30;

            var virtualizedObservableDeque = new VirtualizedConcurrentObservableDeque<int>(pageSize, maintained, obCollection);

            while (Console.ReadLine() != "exit")
            {
                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.Enter:
                        virtualizedObservableDeque.RollDown();
                        PrintEnumerable(virtualizedObservableDeque);
                        break;
                    case ConsoleKey.U:
                        virtualizedObservableDeque.RollUp();
                        PrintEnumerable(virtualizedObservableDeque);
                        break;
                    case ConsoleKey.T:
                        virtualizedObservableDeque.Top();
                        PrintEnumerable(virtualizedObservableDeque);
                        break;
                    case ConsoleKey.E:
                        virtualizedObservableDeque.End();
                        PrintEnumerable(virtualizedObservableDeque);
                        break;
                }
            }
        }

        private static void FillCollections(ICollection<int> observableCollection) => Task.Run(async () =>
        {
            for (var i = 0; i < 10000; i++)
            {
                await Task.Delay(100);
                observableCollection.Add(i);
            }
        });

        private static void PrintEnumerable<T>(IEnumerable<T> deque)
        {
            foreach (var t in deque)
            {
                Console.WriteLine(t);
            }

            Console.WriteLine("-------------------------SPLIT-------------------------");
        }
    }
}
