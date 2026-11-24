namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Models;

    /// <summary>Private connection metadata and current funding selection.</summary>
    public class ProviderConnectionsResponse
    {
        /// <summary>Account-owned connections with secrets excluded from serialization.</summary>
        public List<ProviderConnection> Connections { get; set; } = new List<ProviderConnection>();

        /// <summary>Whether an operator-funded local speech engine is offered to BYOK users.</summary>
        public bool LocalSpeechAvailable { get; set; } = false;

        /// <summary>Effective package and funding mode.</summary>
        public EffectivePlan Plan { get; set; } = new EffectivePlan();

    }
}
