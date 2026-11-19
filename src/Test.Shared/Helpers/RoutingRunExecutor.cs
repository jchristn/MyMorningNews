namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;

    /// <summary>
    /// Executes each run with the executor routed for its topic, or the fallback.
    /// </summary>
    public class RoutingRunExecutor : IRunExecutor
    {
        private readonly ConcurrentDictionary<string, IRunExecutor> _Routes;
        private readonly IRunExecutor _Fallback;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="routes">Routes by topic.</param>
        /// <param name="fallback">Executor for topics without a route.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RoutingRunExecutor(ConcurrentDictionary<string, IRunExecutor> routes, IRunExecutor fallback)
        {
            _Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        /// <inheritdoc />
        public Task<RunResult> ExecuteAsync(Run run, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(run);
            return (_Routes.TryGetValue(run.TopicId, out IRunExecutor? e) ? e : _Fallback).ExecuteAsync(run, token);
        }
    }
}
