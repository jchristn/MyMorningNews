namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Published results for one dataset, or a note saying why there are none.
    /// </summary>
    public class DatasetBaselines
    {
        #region Public-Members

        /// <summary>
        /// Why no comparable published result exists, or other context.
        /// </summary>
        public string Note { get; set; } = String.Empty;

        /// <summary>
        /// Published results.
        /// </summary>
        public List<PublishedBaseline> Published { get; set; } = new List<PublishedBaseline>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load benchmarks/baselines.json and return the entry for a dataset.
        /// </summary>
        /// <param name="dataset">Dataset name.</param>
        /// <returns>Entry; a placeholder note when the file or the entry is missing.</returns>
        public static DatasetBaselines For(string dataset)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            string path = Path.Combine(RepositoryPaths.Benchmarks(), "baselines.json");
            if (!File.Exists(path)) return new DatasetBaselines { Note = "benchmarks/baselines.json is missing." };
            Dictionary<string, DatasetBaselines> all = Json.Deserialize<Dictionary<string, DatasetBaselines>>(File.ReadAllText(path));
            return all.TryGetValue(dataset, out DatasetBaselines? b) ? b : new DatasetBaselines { Note = "No entry for this dataset in benchmarks/baselines.json." };
        }

        #endregion
    }
}
