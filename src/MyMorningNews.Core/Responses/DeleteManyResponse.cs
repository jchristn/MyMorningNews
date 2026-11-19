namespace MyMorningNews.Core.Responses
{

    /// <summary>
    /// Result of a bulk delete.
    /// </summary>
    public class DeleteManyResponse
    {
        /// <summary>
        /// Rows deleted.
        /// </summary>
        public int DeletedCount { get; set; } = 0;
    }
}
