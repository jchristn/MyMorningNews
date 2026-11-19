namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Search provider kind.
    /// </summary>
    public enum SearchProviderKindEnum
    {
        /// <summary>
        /// SearXNG metasearch.
        /// </summary>
        Searxng,

        /// <summary>
        /// Tavily API.
        /// </summary>
        Tavily,

        /// <summary>
        /// You.com API.
        /// </summary>
        YouCom,

        /// <summary>
        /// RSS and Atom feeds from topic constraints.
        /// </summary>
        Rss,

        /// <summary>
        /// Hacker News (Algolia).
        /// </summary>
        HackerNews,

        /// <summary>
        /// arXiv API.
        /// </summary>
        Arxiv,

        /// <summary>
        /// Model-hosted web search.
        /// </summary>
        HostedModelWebSearch
    }
}
