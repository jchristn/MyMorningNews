namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Base class for harness cases: a name, flags for what the case needs, and step tracking.
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// Case name printed in the results.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Whether the case talks to the server. Offline cases run even when the server is down. Default true.
        /// </summary>
        public virtual bool RequiresServer
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Whether the case calls real models or starts research runs, so it runs only with --with-run. Default false.
        /// </summary>
        public virtual bool RequiresRun
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Run the case. Throw HarnessAssertionException to fail and SkipTestException to skip.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result to record steps in.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task RunAsync(TestContext context, TestResult result, CancellationToken token);

        /// <summary>
        /// Run one step, recording its timing and outcome.
        /// </summary>
        /// <typeparam name="T">Step result.</typeparam>
        /// <param name="context">Shared state (for verbose output).</param>
        /// <param name="result">Result to record in.</param>
        /// <param name="step">Step name.</param>
        /// <param name="action">Step.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The step's value.</returns>
        protected static async Task<T> StepAsync<T>(TestContext context, TestResult result, string step, Func<Task<T>> action, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(action);
            ApiDetails details = new ApiDetails { Step = step, StartUtc = DateTime.UtcNow };
            result.ApiDetails.Add(details);
            try
            {
                T value = await action().ConfigureAwait(false);
                details.Success = true;
                return value;
            }
            catch (MyMorningNewsException ex)
            {
                details.StatusCode = ex.StatusCode;
                details.Error = ex.Message;
                throw new HarnessAssertionException(step + " failed: HTTP " + ex.StatusCode + " " + ex.ErrorCode + ": " + ex.Message);
            }
            catch (Exception ex) when (!(ex is HarnessAssertionException) && !(ex is SkipTestException))
            {
                details.Error = ex.Message;
                throw;
            }
            finally
            {
                details.EndUtc = DateTime.UtcNow;
                if (context.Environment.Verbose)
                    Console.WriteLine("       " + (details.Success ? "ok  " : "err ") + step + " (" + (long)details.Runtime.TotalMilliseconds + " ms)");
            }
        }

        /// <summary>
        /// Run one step that returns nothing.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result to record in.</param>
        /// <param name="step">Step name.</param>
        /// <param name="action">Step.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        protected static async Task StepAsync(TestContext context, TestResult result, string step, Func<Task> action, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            await StepAsync<bool>(context, result, step, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, token).ConfigureAwait(false);
        }
    }
}
