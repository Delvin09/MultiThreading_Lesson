namespace MultiThreading_Lesson.Threads
{
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
}
