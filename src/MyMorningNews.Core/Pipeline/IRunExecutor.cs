namespace MyMorningNews.Core.Pipeline
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Executes a claimed run (the research pipeline, plan section 16).
    /// Thread safety: implementations must be safe for concurrent runs.
    /// </summary>
    public interface IRunExecutor
    {
        /// <summary>
        /// Execute a run. Throw to fail it; honor the token for cancellation and the run timeout.
        /// </summary>
        /// <param name="run">Run (status Running).</param>
        /// <param name="token">Cancellation token (cancel request or timeout).</param>
        /// <returns>Result.</returns>
        Task<RunResult> ExecuteAsync(Run run, CancellationToken token);
    }
}
