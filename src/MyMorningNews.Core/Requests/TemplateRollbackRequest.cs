namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Restore an earlier template version.
    /// </summary>
    public class TemplateRollbackRequest
    {
        /// <summary>
        /// Version to restore.
        /// </summary>
        public int Version { get; set; } = 1;
    }
}
