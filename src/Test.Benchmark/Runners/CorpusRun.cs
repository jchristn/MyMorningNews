namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A corpus researched in one arm: the workspace, each round's data, and what went wrong.
    /// </summary>
    public class CorpusRun
    {
        /// <summary>
        /// Workspace.
        /// </summary>
        public Workspace Workspace { get; set; } = new Workspace();

        /// <summary>
        /// Round data by round (only rounds that finished).
        /// </summary>
        public Dictionary<int, RoundData> Rounds { get; set; } = new Dictionary<int, RoundData>();

        /// <summary>
        /// Why a round failed, by round.
        /// </summary>
        public Dictionary<int, string> Failures { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Wall-clock seconds from queueing each round's run to its finish, by round (fresh runs only).
        /// </summary>
        public Dictionary<int, double> RunSeconds { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Whether an existing workspace was reused instead of researched again.
        /// </summary>
        public bool Reused { get; set; } = false;

        /// <summary>
        /// Input that failed to load (makes the report incomplete).
        /// </summary>
        public List<string> IncompleteReasons { get; set; } = new List<string>();
    }
}
