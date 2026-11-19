namespace MyMorningNews.Core.Pipeline
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// One stage of the research pipeline (plan section 16).
    /// Thread safety: stages are stateless; all state lives in the <see cref="RunContext"/>.
    /// </summary>
    public interface IRunStage
    {
        /// <summary>
        /// Stage name recorded on the runstages row.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether a failure fails the run (true) or marks it partially succeeded and continues (false).
        /// </summary>
        bool Critical { get; }

        /// <summary>
        /// Execute the stage.
        /// </summary>
        /// <param name="ctx">Run context.</param>
        /// <param name="services">Pipeline services.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome.</returns>
        Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token);
    }
}
