namespace MultiThreading_Lesson.Threads
{
    class GenRandomArray : GenRandomArray<int>
    {
        public GenRandomArray(int threadCount, int[] resultArray)
            : base(threadCount, resultArray, r => r.Next())
        {
        }
    }
}
