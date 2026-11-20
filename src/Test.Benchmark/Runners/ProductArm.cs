namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A configuration of the product being measured. Arms differ only in per-topic ranking weights, so each runs in its
    /// own workspace with the same data.
    /// </summary>
    public class ProductArm
    {
        #region Public-Members

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Weight overrides (weightLlm, weightFeedback, weightNovelty, weightSource, weightRecency); empty uses the
        /// server defaults.
        /// </summary>
        public SortedDictionary<string, double> Weights { get; set; } = new SortedDictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// The product arms the harness knows.
        /// </summary>
        public static readonly IReadOnlyList<ProductArm> All = new List<ProductArm>
        {
            new ProductArm { Name = "default", Description = "Shipped ranking weights." },
            new ProductArm
            {
                Name = "llm-only",
                Description = "Ablation: the model judgment alone (weightLlm 1, the other components 0).",
                Weights = new SortedDictionary<string, double>(StringComparer.Ordinal) { { "weightLlm", 1 }, { "weightFeedback", 0 }, { "weightNovelty", 0 }, { "weightSource", 0 }, { "weightRecency", 0 } }
            }
        };

        /// <summary>
        /// Reference arms computed by the harness without the product.
        /// </summary>
        public static readonly IReadOnlyList<string> ReferenceArms = new List<string> { "floor-search-order", "reference-lexical" };

        #endregion

        #region Public-Methods

        /// <summary>
        /// A product arm by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Arm, or null.</returns>
        public static ProductArm? Find(string name)
        {
            return All.FirstOrDefault(a => String.Equals(a.Name, name, StringComparison.Ordinal));
        }

        /// <summary>
        /// A stable description of the settings, for workspace hashing.
        /// </summary>
        /// <returns>Text.</returns>
        public string Fingerprint()
        {
            return Name + ":" + String.Join(",", Weights.Select(kv => kv.Key + "=" + kv.Value.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        #endregion
    }
}
