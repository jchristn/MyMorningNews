namespace Test.Xunit3
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Runs each shared case as its own xUnit v3 theory row.
    /// </summary>
    public sealed class MyMorningNewsTheoryTests
    {
        /// <summary>
        /// Case source.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit3Case> Cases()
        {
            return TouchstoneXunit3Cases.From<MyMorningNewsSuites>();
        }

        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">Case.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit3Case testCase)
        {
            return TouchstoneXunit3.ExecuteAsync(testCase, TestContext.Current.CancellationToken);
        }
    }
}
