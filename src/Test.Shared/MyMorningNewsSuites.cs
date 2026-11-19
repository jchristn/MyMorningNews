namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Registry of every shared test suite. Consumed by Test.Automated and Test.Xunit3.
    /// </summary>
    public sealed class MyMorningNewsSuites : ITestSuiteProvider
    {
        /// <summary>
        /// Every suite, freshly built on each access.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    SecuritySuite.Create(),
                    SettingsSuite.Create(),
                    DatabaseSuite.Create(),
                    AuthenticationSuite.Create(),
                    AuthorizationSuite.Create(),
                    ResourceSuite.Create(),
                    ObservabilitySuite.Create(),
                    ModelSuite.Create(),
                    MobileSuite.Create(),
                    TopicSuite.Create(),
                    ScheduleSuite.Create(),
                    SearchSuite.Create(),
                    PipelineSuite.Create(),
                    StorageSuite.Create(),
                    BriefingSuite.Create(),
                    MediaSuite.Create(),
                    EmailSuite.Create()
                };
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return All; }
        }
    }
}
