namespace MyMorningNews.Core.Responses
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Cost of the calls made for one task role in a run.
    /// </summary>
    public class TaskCost : CostTotals
    {
        /// <summary>
        /// Task role.
        /// </summary>
        public ModelTaskEnum Task { get; set; } = ModelTaskEnum.Synthesis;
    }
}
