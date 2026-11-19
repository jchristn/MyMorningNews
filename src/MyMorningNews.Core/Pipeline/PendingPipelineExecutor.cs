namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Placeholder executor used until the research pipeline stages are installed (Phase 6). Runs finish immediately with
    /// a summary that says so, and nothing is delivered.
    /// Thread safety: thread safe.
    /// </summary>
    public class PendingPipelineExecutor : IRunExecutor
    {
        /// <inheritdoc />
        public Task<RunResult> ExecuteAsync(Run run, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(run);
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new RunResult { Summary = "Queued and executed. The research stages (search, read, rank, brief) are not installed on this server version yet.", Delivered = false });
        }
    }
}
