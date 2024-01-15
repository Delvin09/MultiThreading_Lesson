using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Channels;

namespace MultiThreading_Lesson
{
    class SingleTaskScheduler : TaskScheduler
    {
        private readonly object sync = new object();

        private readonly Queue<Task> tasks = new Queue<Task>();

        private Thread thread;

        public SingleTaskScheduler()
        {
            thread = new Thread(ExcecuteTask) { Name = "OneForAll", IsBackground = true };
            thread.Start();
        }

        protected override IEnumerable<Task>? GetScheduledTasks()
        {
            return tasks;
        }

        protected override void QueueTask(Task task)
        {
            lock (sync)
            {
                tasks.Enqueue(task);
            }
        }

        private void ExcecuteTask(object? obj)
        {
            while (true)
            {
                lock (sync)
                {
                    if (tasks.Count > 0)
                    {
                        var task = tasks.Peek();
                        if (TryDequeue(task))
                            TryExecuteTask(task);
                    }
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override bool TryDequeue(Task task)
        {
            Task currentTask;
            lock (sync)
            {
                currentTask = tasks.Dequeue();
            }

            return currentTask == task;
        }
    }

    class MultiTaskRandomProcessor<T>
    {
        private readonly Task[] _tasks;
        private readonly T[] _array;
        private readonly Func<Random, T> _randomize;
        protected readonly Random[] _randoms;

        public MultiTaskRandomProcessor(int threadCount, T[] array, Func<Random, T> randomize)
        {
            _tasks = new Task[threadCount];
            _array = array;
            _randomize = randomize;
            _randoms = new Random[threadCount];
        }

        public virtual Task Process(CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _randoms.Length; i++)
            {
                _randoms[i] = new Random();
            }
            for (int i = 0; i < _tasks.Length; i++)
            {
                var index = i;
                _tasks[i] = Task.Run(() => ThreadProc(index, cancellationToken), cancellationToken);
            }

            return Task.WhenAll(_tasks);
        }

        private void ThreadProc(int threadIndex, CancellationToken cancellationToken = default)
        {
            var length = _tasks.Length;
            var index = threadIndex;
            var count = _array.Length / length;

            var span = index == length - 1
                ? _array.AsSpan((index * count)..)
                : _array.AsSpan((index * count)..((index * count) + count));

            for (var i = 0; !cancellationToken.IsCancellationRequested && i < span.Length; i++)
            {
                ProcessValue(index, i, span);
            }
        }

        protected void ProcessValue(int threadIndex, int itemIndex, Span<T> span)
        {
            span[itemIndex] = _randomize(_randoms[threadIndex]);
        }
    }

    abstract class MultiThreadingProcessor<T>
    {
        private readonly Thread[] _threads;
        private readonly T[] _array;

        public MultiThreadingProcessor(int threadCount, T[] array)
        {
            _threads = new Thread[threadCount];
            _array = array;
        }

        public virtual void Process()
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(ThreadProc) { IsBackground = true };
                _threads[i].Start(i);
            }

            foreach (var thread in _threads)
                thread.Join();
        }

        private void ThreadProc(object? state)
        {
            var length = _threads.Length;
            var index = (int)state!;
            var count = _array.Length / length;

            var span = index == length - 1
                ? _array.AsSpan((index * count)..)
                : _array.AsSpan((index * count)..((index * count) + count));

            for (var i = 0; i < span.Length; i++)
            {
                ProcessValue(index, i, span);
            }
        }

        protected abstract void ProcessValue(int threadIndex, int itemIndex, Span<T> span);
    }

    class GenRandomArray : GenRandomArray<int>
    {
        public GenRandomArray(int threadCount, int[] resultArray)
            : base(threadCount, resultArray, r => r.Next())
        {
        }
    }

    class GenRandomArray<T> : MultiThreadingProcessor<T>
    {
        private readonly Func<Random, T> _randomize;
        protected readonly Random[] _randoms;

        public GenRandomArray(int threadCount, T[] array, Func<Random, T> randomize)
            : base(threadCount, array)
        {
            this._randomize = randomize;
            _randoms = new Random[threadCount];
        }

        public override void Process()
        {
            for (var i = 0; i < _randoms.Length; i++)
            {
                _randoms[i] = new Random();
            }

            base.Process();
        }

        protected override void ProcessValue(int threadIndex, int itemIndex, Span<T> span)
        {
            span[itemIndex] = _randomize(_randoms[threadIndex]);
        }
    }

    class SumSearch : MultiThreadingProcessor<int>
    {
        private readonly long[] _results;

        public long Result { get; private set; }

        public SumSearch(int threadCount, int[] array)
            : base(threadCount, array)
        {
            _results = new long[threadCount];
        }

        public override void Process()
        {
            base.Process();
            Result = _results.Sum();
        }

        protected override void ProcessValue(int threadIndex, int itemIndex, Span<int> span)
        {
           _results[threadIndex] += span[itemIndex];
        }
    }

    class FreqChar : MultiThreadingProcessor<char>
    {
        private readonly Dictionary<char, int>[] _results;

        public Dictionary<char, int>? Result { get; private set; }

        public FreqChar(int threadCount, char[] array)
            : base(threadCount, array)
        {
            _results = new Dictionary<char, int>[threadCount];
        }

        public override void Process()
        {
            base.Process();
            Result = new Dictionary<char, int>();

            foreach (var item in _results)
            {
                foreach (var pair in item)
                {
                    if (Result.TryGetValue(pair.Key, out int value))
                    {
                        Result[pair.Key] = value + pair.Value;
                    }
                    else
                    {
                        Result[pair.Key] = pair.Value;
                    }
                }
            }
        }

        protected override void ProcessValue(int threadIndex, int itemIndex, Span<char> span)
        {
            var ch = span[itemIndex];
            var dic = _results[threadIndex];
            if (dic == null)
            {
                _results[threadIndex] = dic = new Dictionary<char, int>();
            }

            if (dic.TryGetValue(ch, out int value))
            {
                dic[ch] = value + 1;
            }
            else
            {
                dic[ch] = 1;
            }
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            var cancel = new CancellationTokenSource();

            const int threadCount = 10;
            var arr = new int[1_000_000_000];
            var taskRandom = new MultiTaskRandomProcessor<int>(threadCount, arr, r => r.Next());
            var task = taskRandom.Process(cancel.Token);

            Console.WriteLine("Random is in process");
            Console.WriteLine("For cancel press ESC");
            while (!cancel.IsCancellationRequested && !task.IsCompleted)
            {
                var ch = Console.ReadKey();
                if (ch.Key == ConsoleKey.Escape) cancel.Cancel();
            }

            // ============================================================================
            var gen = new GenRandomArray(threadCount, arr);
            var sw = Stopwatch.StartNew();
            gen.Process();
            Console.WriteLine($"--> {sw.Elapsed}");

            var sumProcessor = new SumSearch(threadCount, arr);
            sw = Stopwatch.StartNew();
            sumProcessor.Process();
            Console.WriteLine($"--> {sw.Elapsed} --- result: {sumProcessor.Result}");


            var charArray = new char[1_000_000_000];
            var chGen = new GenRandomArray<char>(threadCount, charArray, r => (char)r.Next(32, 58));
            chGen.Process();

            var freqDicProc = new FreqChar(threadCount, charArray);

            sw = Stopwatch.StartNew();
            freqDicProc.Process();
            Console.WriteLine($"--> {sw.Elapsed} --- result: {freqDicProc.Result!.Count}");
        }
    }
}
