using System.Diagnostics;
using System.Drawing;
using System.Threading.Channels;

namespace MultiThreading_Lesson
{
    class SomeClasse
    {
        private readonly object sync = new object();

        private int sum = 0;

        public int Sum => sum;

        public bool IsCanceled { get; set; }

        public void ProcThread()
        {
            for (int i = 0; i < 100_000_000; i++)
                if (IsCanceled)
                    break;
                else
                    sum++;
        }
    }

    class GenRandomArray
    {
        private Thread[] threads;
        private int[] result;
        private Random[] randoms;

        public GenRandomArray(int threadCount, int[] resultArray)
        {
            threads = new Thread[threadCount];
            result = resultArray;
            randoms = new Random[threadCount];
        }

        public void Process()
        {
            for (int i = 0; i < threads.Length; i++)
            {
                randoms[i] = new Random();
                threads[i] = new Thread(ThreadProc);
                threads[i].Start(i);
            }

            foreach (var thread in threads) thread.Join();
        }

        private void ThreadProc(object? state)
        {
            var length = threads.Length;
            var index = (int)state;
            var count = result.Length / length;

            var span = index == length - 1
                ? result.AsSpan((index * count)..)
                : result.AsSpan((index*count)..((index * count) + count));

            for(var i = 0; i < span.Length; i++)
            {
                span[i] = randoms[index].Next();
            }
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            //var obj = new SomeClasse();
            //var obj2 = new SomeClasse();
            //var obj3 = new SomeClasse();
            //var obj4 = new SomeClasse();

            //var thread = new Thread(obj.ProcThread) { IsBackground = true };
            //var thread2 = new Thread(obj2.ProcThread) { IsBackground = true };
            //var thread3 = new Thread(obj3.ProcThread) { IsBackground = true };
            //var thread4 = new Thread(obj4.ProcThread) { IsBackground = true };

            var arr = new int[1_000];
            var gen = new GenRandomArray(1, arr);

            // 1 - 00:00:05.1498743
            // 2 - 00:00:02.6744600
            // 3 - 00:00:01.8015496
            // 4 - 00:00:01.5142132
            // 5 - 00:00:01.3053252
            // 8 - 00:00:00.9570645
            // 16 - 00:00:00.7248890
            // 32 - 00:00:00.7853518

            var sw = Stopwatch.StartNew();
            gen.Process();
            Console.WriteLine($"--> {sw.Elapsed}");

            //Without threads
            // 00:00:00.0549608
            // 00:00:00.0809175
            // 00:00:00.0635487

            // With threads and WIthout LOCK
            // 00:00:00.4483470
            // 00:00:00.4645914
            // 00:00:00.4451844

            //With LOCK
            // 00:00:05.6916445
            // 00:00:06.6426243
            // 00:00:05.9325970

            // 00:00:01.2150220
            // 00:00:01.2147960
            // 00:00:01.2144526
        }
    }
}
