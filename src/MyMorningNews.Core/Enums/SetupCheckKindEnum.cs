namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What a setup check covers.
    /// </summary>
    public enum SetupCheckKindEnum
    {
        /// <summary>
        /// A chat model endpoint that serves one or more tasks. Required.
        /// </summary>
        Model,

        /// <summary>
        /// Web search. Required.
        /// </summary>
        Search,

        /// <summary>
        /// Embeddings for duplicate detection and ranking. Optional.
        /// </summary>
        Embeddings,

        /// <summary>
        /// Email delivery. Optional.
        /// </summary>
        Email
    }
}
