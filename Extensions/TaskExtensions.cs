using System.Threading;
using System.Threading.Tasks;

namespace DCI.SystemEvents.Extensions
{
    static class TaskExtensions
    {
        public static Task AsTask(this CancellationToken cancellationToken, bool useContext = false)
        {
            var tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: useContext);
            return tcs.Task;
        }
    }
}