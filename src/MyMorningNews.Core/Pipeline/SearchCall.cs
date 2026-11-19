namespace MyMorningNews.Core.Pipeline
{
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;

    /// <summary>
    /// One planned call: a query against a provider.
    /// </summary>
    public class SearchCall
    {
        /// <summary>
        /// Provider.
        /// </summary>
        public SearchProvider Provider { get; set; } = new SearchProvider();

        /// <summary>
        /// Query.
        /// </summary>
        public PlannedQuery Query { get; set; } = new PlannedQuery();
    }
}
