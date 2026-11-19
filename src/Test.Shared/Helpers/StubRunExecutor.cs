namespace Test.Shared.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;

    /// <summary>
    /// A run executor whose behavior a test supplies. By default it succeeds with summary "ok".
    /// </summary>
    public class StubRunExecutor : IRunExecutor
    {
        /// <summary>
        /// What a run does.
        /// </summary>
        public Func<Run, CancellationToken, Task<RunResult>> Body { get; set; } = (r, t) => Task.FromResult(new RunResult { Summary = "ok", Delivered = true });

        /// <inheritdoc />
        public Task<RunResult> ExecuteAsync(Run run, CancellationToken token)
        {
            return Body(run, token);
        }
    }
}
