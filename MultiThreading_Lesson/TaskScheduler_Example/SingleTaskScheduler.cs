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
}
