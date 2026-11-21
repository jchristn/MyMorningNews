namespace MyMorningNews.Core.Services
{
    using System;
    using System.Threading;

    /// <summary>
    /// The run stage that model calls on this logical call path belong to. The pipeline opens a scope around each stage,
    /// and <see cref="ModelGateway"/> stamps every call it logs with the stage, so cost and tokens add up per stage.
    /// Thread safety: the current stage flows with async calls (AsyncLocal) and never leaks between runs.
    /// </summary>
    public sealed class RunStageScope : IDisposable
    {
        #region Private-Members

        private static readonly AsyncLocal<string?> _Current = new AsyncLocal<string?>();
        private readonly string? _Previous;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        private RunStageScope(string runStageId)
        {
            _Previous = _Current.Value;
            _Current.Value = runStageId;
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Run stage of the current call path, or null outside a stage.
        /// </summary>
        public static string? Current
        {
            get { return _Current.Value; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Attribute model calls on this call path to a run stage until the scope is disposed.
        /// </summary>
        /// <param name="runStageId">Run stage.</param>
        /// <returns>Scope; dispose it when the stage ends.</returns>
        /// <exception cref="ArgumentNullException">runStageId is null or empty.</exception>
        public static RunStageScope Begin(string runStageId)
        {
            if (String.IsNullOrWhiteSpace(runStageId)) throw new ArgumentNullException(nameof(runStageId));
            return new RunStageScope(runStageId);
        }

        /// <summary>
        /// Restore the previous stage.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Current.Value = _Previous;
            _Disposed = true;
        }

        #endregion
    }
}
