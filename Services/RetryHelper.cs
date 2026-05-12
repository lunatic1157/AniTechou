using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    public static class RetryHelper
    {
        /// <summary>
        /// Execute an async operation with exponential backoff retry.
        /// Only retries on transient errors (HttpRequestException, TaskCanceledException).
        /// </summary>
        public static async Task<T> RetryAsync<T>(Func<Task<T>> action, string context, int maxRetries = 2)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    attempt++;
                    if (attempt > maxRetries) throw;

                    int delayMs = attempt * 1000;
                    System.Diagnostics.Debug.WriteLine(
                        $"[Retry] {context} 第{attempt}次重试，等待{delayMs}ms（{ex.GetType().Name}: {ex.Message}）");
                    await Task.Delay(delayMs);
                }
            }
        }
    }
}
