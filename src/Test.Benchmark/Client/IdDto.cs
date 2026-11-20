namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// Any record with an id and a name (tenants, endpoints, providers, plans, dimensions).
    /// </summary>
    public class IdDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Name, when the record has one.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Endpoint URL, for endpoints and providers.
        /// </summary>
        public string? Endpoint { get; set; } = null;
    }
}
