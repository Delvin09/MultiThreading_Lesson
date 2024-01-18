namespace MultiThreading_Lesson.Threads
{
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
                : _array.AsSpan((index * count)..(index * count + count));

            for (var i = 0; i < span.Length; i++)
            {
                ProcessValue(index, i, span);
            }
        }

        protected abstract void ProcessValue(int threadIndex, int itemIndex, Span<T> span);
    }
}
