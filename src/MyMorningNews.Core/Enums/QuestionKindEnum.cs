namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Refinement question kind.
    /// </summary>
    public enum QuestionKindEnum
    {
        /// <summary>
        /// Pick one.
        /// </summary>
        SingleChoice,

        /// <summary>
        /// Pick any.
        /// </summary>
        MultiChoice,

        /// <summary>
        /// Scale from 1 to 5.
        /// </summary>
        Scale,

        /// <summary>
        /// Yes or no.
        /// </summary>
        YesNo,

        /// <summary>
        /// Free text.
        /// </summary>
        FreeText
    }
}
