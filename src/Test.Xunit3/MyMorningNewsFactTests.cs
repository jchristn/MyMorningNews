namespace Test.Xunit3
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Runs every shared case inside one xUnit v3 fact.
    /// </summary>
    public sealed class MyMorningNewsFactTests : TouchstoneXunit3FactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return MyMorningNewsSuites.All; }
        }

        /// <summary>
        /// Run every suite.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public Task RunAll()
        {
            return RunAllAsync(TestContext.Current.CancellationToken);
        }
    }
}
