namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What started a run.
    /// </summary>
    public enum RunTriggerEnum
    {
        /// <summary>
        /// A schedule.
        /// </summary>
        Schedule,

        /// <summary>
        /// Run now.
        /// </summary>
        Manual,

        /// <summary>
        /// A one-time request.
        /// </summary>
        Request,

        /// <summary>
        /// Fresh research before an edition.
        /// </summary>
        Edition,

        /// <summary>
        /// Wizard preview.
        /// </summary>
        Preview
    }
}
