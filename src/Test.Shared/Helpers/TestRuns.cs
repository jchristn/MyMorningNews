namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Server;

    /// <summary>
    /// Routes queued runs to the executor of the test that owns their topic. The run queue is shared by every suite, so a
    /// suite draining it in parallel can claim another suite's run; routing keeps each run on the executor its test chose.
    /// Topics without a route use the server's real pipeline. Thread safety: safe for concurrent use.
    /// </summary>
    public static class TestRuns
    {
        private static readonly ConcurrentDictionary<string, IRunExecutor> _Routes = new ConcurrentDictionary<string, IRunExecutor>(StringComparer.Ordinal);

        /// <summary>
        /// Send runs of a topic to an executor.
        /// </summary>
        /// <param name="topicId">Topic.</param>
        /// <param name="executor">Executor.</param>
        public static void Route(string topicId, IRunExecutor executor)
        {
            ArgumentNullException.ThrowIfNull(executor);
            _Routes[topicId] = executor;
        }

        /// <summary>
        /// The executor to pass to ExecuteQueuedAsync.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <returns>Routing executor.</returns>
        public static IRunExecutor Executor(MyMorningNewsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            return new RoutingRunExecutor(_Routes, server.Context.RunExecutor);
        }
    }
}
