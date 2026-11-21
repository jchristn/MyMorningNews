namespace MyMorningNews.Core.Responses
{
    /// <summary>
    /// Cost of the calls one model made in a run.
    /// </summary>
    public class ModelCost : CostTotals
    {
        /// <summary>
        /// Model endpoint, when it still exists.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Model endpoint name, when it still exists.
        /// </summary>
        public string? EndpointName { get; set; } = null;

        /// <summary>
        /// Provider.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Model.
        /// </summary>
        public string? Model { get; set; } = null;
    }
}
