namespace MultiThreading_Lesson.Threads
{
    class GenRandomArray<T> : MultiThreadingProcessor<T>
    {
        private readonly Func<Random, T> _randomize;
        protected readonly Random[] _randoms;

        public GenRandomArray(int threadCount, T[] array, Func<Random, T> randomize)
            : base(threadCount, array)
        {
            _randomize = randomize;
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
}
