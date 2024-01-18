using MultiThreading_Lesson.Threads;
using System.Diagnostics;
using System.Linq;

namespace MultiThreading_Lesson
{
    interface ITaskProcessor
    {
        Task Process(CancellationToken cancellationToken = default);
    }

    interface ITaskProcessor<TResult>
    {
        Task<TResult> Process(CancellationToken cancellationToken = default);
    }

    abstract class MultiTaskProcessorBase<TItem> : ITaskProcessor
    {
        protected readonly Task[] _tasks;
        private readonly TItem[] _array;

        public MultiTaskProcessorBase(int taskCount, TItem[] array)
        {
            _tasks = new Task[taskCount];
            _array = array;
        }

        public virtual Task Process(CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < _tasks.Length; i++)
            {
                var length = _tasks.Length;
                var index = i;
                var count = _array.Length / length;

                var memory = index == length - 1
                    ? _array.AsMemory((index * count)..)
                    : _array.AsMemory((index * count)..((index * count) + count));

                _tasks[i] = CreateAndRunTask(index, memory, cancellationToken);
            }

            return Task.WhenAll(_tasks);
        }

        protected abstract Task CreateAndRunTask(int taskIndex, Memory<TItem> items, CancellationToken cancellationToken = default);
    }

    abstract class MultiTaskProcessor<TItem> : MultiTaskProcessorBase<TItem>
    {
        private readonly TaskFactory _taskFactory;

        protected MultiTaskProcessor(int threadCount, TItem[] array)
            : base(threadCount, array) {

            _taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
        }

        protected override Task CreateAndRunTask(int taskIndex, Memory<TItem> items, CancellationToken cancellationToken = default)
            => _taskFactory.StartNew(() => ProcessPartArray(taskIndex, items, cancellationToken), cancellationToken);

        private void ProcessPartArray(int taskIndex, Memory<TItem> items, CancellationToken cancellationToken = default)
        {
            var span = items.Span;
            for (var i = 0; !cancellationToken.IsCancellationRequested && i < span.Length; i++)
                ProcessItem(taskIndex, i, span);
        }

        protected abstract void ProcessItem(int taskIndex, int itemIndex, Span<TItem> span);
    }

    abstract class MultiTaskProcessor<TItem, TResult> : MultiTaskProcessorBase<TItem>, ITaskProcessor<TResult>
    {
        private readonly TaskFactory<TResult> _tasksFactory;

        protected MultiTaskProcessor(int threadCount, TItem[] array)
            : base(threadCount, array)
        {
            _tasksFactory = new TaskFactory<TResult>(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
        }

        public override Task<TResult> Process(CancellationToken cancellationToken = default)
            => base.Process(cancellationToken)
                .ContinueWith(t => HandleResults(_tasks.OfType<Task<TResult>>()), cancellationToken);

        protected override Task CreateAndRunTask(int taskIndex, Memory<TItem> items, CancellationToken cancellationToken = default)
            => _tasksFactory.StartNew(() => ProcessPartArray(taskIndex, items, cancellationToken)!, cancellationToken);

        private TResult? ProcessPartArray(int taskIndex, Memory<TItem> items, CancellationToken cancellationToken = default)
        {
            TResult? result = default;
            var span = items.Span;
            for (var i = 0; !cancellationToken.IsCancellationRequested && i < span.Length; i++)
                result = ProcessItem(taskIndex, i, span, result);
            return result;
        }

        protected abstract TResult? ProcessItem(int taskIndex, int itemIndex, Span<TItem> span, TResult? result);


        protected abstract TResult HandleResults(IEnumerable<Task<TResult>> enumerable);
    }

    // ===========================

    class MultiTaskRandomProcessor<T> : MultiTaskProcessor<T>
    {
        private readonly Func<Random, T> _randomize;
        protected readonly Random[] _randoms;

        public MultiTaskRandomProcessor(int threadCount, T[] array, Func<Random, T> randomize)
            : base(threadCount, array)
        {
            _randomize = randomize;
            _randoms = new Random[threadCount];

            var r = new Random();
            for (var i = 0; i < threadCount; i++)
            {
                _randoms[i] = new Random(r.Next());
            }
        }

        protected override void ProcessItem(int taskIndex, int itemIndex, Span<T> span)
        {
            span[itemIndex] = _randomize(_randoms[taskIndex]);
        }
    }

    class MultiTaskSumProcessor : MultiTaskProcessor<int, long>
    {
        public MultiTaskSumProcessor(int threadCount, int[] array) : base(threadCount, array)
        {
        }

        protected override long HandleResults(IEnumerable<Task<long>> enumerable)
        {
            return enumerable.Select(t => t.Result).Sum();
        }

        protected override long ProcessItem(int taskIndex, int itemIndex, Span<int> span, long result)
        {
            return result + span[itemIndex];
        }
    }

    class MultiTaskMinProcessor : MultiTaskProcessor<int, int>
    {
        public MultiTaskMinProcessor(int threadCount, int[] array)
            : base(threadCount, array)
        {
        }

        protected override int HandleResults(IEnumerable<Task<int>> enumerable)
        {
            return enumerable.Select(t => t.Result).Min();
        }

        protected override int ProcessItem(int taskIndex, int itemIndex, Span<int> span, int result)
        {
            return result > span[itemIndex] ? span[itemIndex] : result;
        }
    }

    class MultiTaskMaxProcessor : MultiTaskProcessor<int, int>
    {
        public MultiTaskMaxProcessor(int threadCount, int[] array)
            : base(threadCount, array)
        {
        }

        protected override int HandleResults(IEnumerable<Task<int>> enumerable)
        {
            return enumerable.Select(t => t.Result).Max();
        }

        protected override int ProcessItem(int taskIndex, int itemIndex, Span<int> span, int result)
        {
            return result < span[itemIndex] ? span[itemIndex] : result;
        }
    }

    class MultiTaskCharProcessor : MultiTaskProcessor<char, Dictionary<char, int>>
    {
        public MultiTaskCharProcessor(int threadCount, char[] array) : base(threadCount, array)
        {
        }

        protected override Dictionary<char, int> HandleResults(IEnumerable<Task<Dictionary<char, int>>> enumerable)
        {
            var result = new Dictionary<char, int>();

            foreach (var item in enumerable.Select(t => t.Result))
            {
                foreach (var pair in item)
                {
                    if (result.TryGetValue(pair.Key, out int value))
                    {
                        result[pair.Key] = value + pair.Value;
                    }
                    else
                    {
                        result[pair.Key] = pair.Value;
                    }
                }
            }

            return result;
        }

        protected override Dictionary<char, int>? ProcessItem(int taskIndex, int itemIndex, Span<char> span, Dictionary<char, int>? result)
        {
            if (result == null)
                result = new Dictionary<char, int>();

            var ch = span[itemIndex];
            if (result.TryGetValue(ch, out int value))
            {
                result[ch] = value + 1;
            }
            else
            {
                result[ch] = 1;
            }

            return result;
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            const int taskCount = 4;
            Console.CursorVisible = false;

            var sw = Stopwatch.StartNew();

            var t1 = StartIntBlock(taskCount);
            var t2 = StartCharBlock(taskCount);

            await Task.WhenAll(t1, t2);

            sw.Stop();
            Console.SetCursorPosition(0, 10);
            Console.WriteLine($"Done! Total time {sw.Elapsed}");

            Console.WriteLine($"Sum: {t1.Result.sum}, Min: {t1.Result.min}, Max: {t1.Result.max}, Chars: {t2.Result.Count}");

            // ============================================================================
            //MultiThreadind_Work(threadCount);
        }

        static object consoleSync = new object();

        static async Task<(long sum, int min, int max)> StartIntBlock(int taskCount)
        {
            var arr = new int[1_000_000_000];
            var intRandomProcessor = new MultiTaskRandomProcessor<int>(taskCount, arr, r => r.Next());

            await StartProcess(0, "Gen random ints", intRandomProcessor);

            var sumProcessor = new MultiTaskSumProcessor(taskCount, arr);
            var minProcessor = new MultiTaskMinProcessor(taskCount, arr);
            var maxProcessor = new MultiTaskMaxProcessor(taskCount, arr);

            var sum = StartProcess<long>(1, "\tSearch sum", sumProcessor);
            var min = StartProcess<int>(2, "\tSearch min", minProcessor);
            var max = StartProcess<int>(3, "\tSearch max", maxProcessor);

            await Task.WhenAll(sum, min, max);
            return (sum.Result, min.Result, max.Result);
        }

        static async Task<Dictionary<char, int>> StartCharBlock(int taskCount)
        {
            var chars = new char[1_000_000_000];
            var charRandomProcessor = new MultiTaskRandomProcessor<char>(taskCount, chars, r => (char)r.Next(32, 58));
            var charProcessor = new MultiTaskCharProcessor(taskCount, chars);

            await StartProcess(4, "Gen random chars", charRandomProcessor);
            return await StartProcess<Dictionary<char, int>>(5, "\tSearch chars dictionary", charProcessor);
        }

        static string[] dots = new string[] { ".", "..", "...", "....", "....." };

        static async Task StartProcess(int line, string name, ITaskProcessor processor)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            Stopwatch sw = new Stopwatch();

            var progressTask = Task.Run(() =>
            {
                var i = 0;
                var token = cancel.Token;
                while (!token.IsCancellationRequested)
                {
                    Print(line, $"Start {name}{dots[i]}");
                    i++;
                    if (i >= dots.Length) i = 0;

                    try
                    {
                        Task.Delay(150).Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }, cancel.Token);

            sw.Start();
            await processor.Process();
            sw.Stop();

            cancel.Cancel();
            await progressTask;

            Print(line, $"{name} ---> {sw.Elapsed}");
        }

        static async Task<TResult> StartProcess<TResult>(int line, string name, ITaskProcessor<TResult> processor)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            Stopwatch sw = new Stopwatch();

            var progressTask = Task.Run(() =>
            {
                var i = 0;
                var token = cancel.Token;
                while (!token.IsCancellationRequested)
                {
                    Print(line, $"Start {name}{dots[i]}");
                    i++;
                    if (i >= dots.Length) i = 0;

                    try
                    {
                        Task.Delay(150).Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }, cancel.Token);

            sw.Start();
            var result = await processor.Process();
            sw.Stop();

            cancel.Cancel();
            await progressTask;

            Print(line, $"{name} ---> {sw.Elapsed}{GetResultForPrint(result)}");
            return result;
        }

        private static int GetCount(System.Collections.IEnumerable collection)
        {
            var enumerator = collection.GetEnumerator();
            var count = 0;
            while (enumerator.MoveNext()) count++;
            return count;
        } 

        private static string GetResultForPrint<TResult>(TResult? result)
        {
            if (result is System.Collections.IEnumerable collection)
            {
                return $"Count: {GetCount(collection)}";
            }
            return result?.ToString() ?? string.Empty;
        }

        private static void Print(int line, string text)
        {
            lock (consoleSync)
            {
                Console.SetCursorPosition(0, line);
                Console.WriteLine("                                              ");
                Console.SetCursorPosition(0, line);
                Console.WriteLine(text);
            }
        }

        private static void MultiThreadind_Work(int threadCount)
        {
            var arr = new int[1_000_000_000];

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
