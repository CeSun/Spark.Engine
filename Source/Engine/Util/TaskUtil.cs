using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util
{
    public static class TaskUtil
    {
        public static void Then<T>(this Task<T> task, Action<T> action, Action<Exception>? exception = null) 
        {
            task.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    action?.Invoke(task.Result);
                }

                else if (task.IsFaulted)
                {
                    Exception? ex = task.Exception;
                    if (ex == null)
                        ex = new Exception("未知异步错误");
                    exception?.Invoke(ex);
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }


        public static void Then(this Task task, Action action, Action<Exception>? exception = null)
        {
            task.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    action?.Invoke();
                }
                else if (task.IsFaulted)
                {
                    Exception? ex = task.Exception;
                    if (ex == null)
                        ex = new Exception("未知异步错误");
                    exception?.Invoke(ex);
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

    }
}
