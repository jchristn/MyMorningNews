namespace Test.Shared.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Shorthand for building case descriptors.
    /// </summary>
    public static class Cases
    {
        /// <summary>
        /// Build a case.
        /// </summary>
        /// <param name="suiteId">Suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="execute">Body.</param>
        /// <param name="tags">Tags.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> execute, params string[] tags)
        {
            return new TestCaseDescriptor(suiteId: suiteId, caseId: caseId, displayName: displayName, executeAsync: execute, tags: tags.Length > 0 ? tags : null);
        }
    }
}
