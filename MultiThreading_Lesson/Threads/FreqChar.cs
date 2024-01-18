namespace MultiThreading_Lesson.Threads
{
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
}
