namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Pin or hide a finding. Omitted fields are left as they are.
    /// </summary>
    public class FindingUpdateRequest
    {
        /// <summary>
        /// Pin (true) or unpin (false). Null leaves it.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Hide (true) or show (false). Null leaves it.
        /// </summary>
        public bool? Hidden { get; set; } = null;
    }
}
