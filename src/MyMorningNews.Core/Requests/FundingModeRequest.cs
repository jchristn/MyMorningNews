namespace MyMorningNews.Core.Requests
{
    /// <summary>Choose Personal for BYOK or Platform to restore the account's assigned package. Existing subscriptions must be managed separately.</summary>
    public class FundingModeRequest
    {
        /// <summary>Personal or Platform. Default Personal.</summary>
        public string Mode { get; set; } = "Personal";
    }
}
