namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Offline: list options clamp paging and format typed filters the way the server parses them.
    /// </summary>
    public class ListOptionsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name
        {
            get
            {
                return "Offline: list options and query formatting";
            }
        }

        /// <summary>
        /// Offline.
        /// </summary>
        public override bool RequiresServer
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ListOptionsTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            FindingListOptions o = new FindingListOptions
            {
                PageNumber = 0,
                PageSize = 5000,
                SortDirection = SortDirectionEnum.Ascending,
                Tier = FindingTierEnum.MustRead,
                IncludeDuplicates = true,
                MinScore = 70,
                FromUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                Mine = true
            };
            Dictionary<string, string> q = o.ToQuery();
            Helpers.Equal("1", q["pageNumber"], "page number clamps to 1");
            Helpers.Equal("1000", q["pageSize"], "page size clamps to 1000");
            Helpers.Equal("Ascending", q["sortDirection"], "sort direction");
            Helpers.Equal("MustRead", q["tier"], "tier");
            Helpers.Equal("true", q["includeDuplicates"], "boolean");
            Helpers.Equal("70", q["minScore"], "integer");
            Helpers.Equal("true", q["mine"], "owner filter");
            Helpers.True(q["fromUtc"].StartsWith("2026-01-02T03:04:05", StringComparison.Ordinal) && q["fromUtc"].EndsWith("Z", StringComparison.Ordinal), "ISO 8601 UTC date");
            Helpers.True(!q.ContainsKey("topicId") && !q.ContainsKey("search"), "null filters are left out");

            ByteRange range = new ByteRange(0, 1023);
            Helpers.Equal("bytes=0-1023", range.ToHeaderValue(), "range header");
            Helpers.Equal("bytes=10-", new ByteRange(10).ToHeaderValue(), "open range header");
            return Task.CompletedTask;
        }
    }
}
