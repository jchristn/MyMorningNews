namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.IO;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// Loads a dataset file and fingerprints it with SHA-256 so two reports that claim the same dataset can be checked.
    /// </summary>
    public class DatasetLoader
    {
        #region Public-Members

        /// <summary>
        /// Absolute path of the loaded file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Lowercase hex SHA-256 of the file.
        /// </summary>
        public string Sha256 { get; }

        /// <summary>
        /// The dataset.
        /// </summary>
        public BenchmarkDataset Dataset { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Load a dataset.
        /// </summary>
        /// <param name="path">Path, absolute or relative to the repository root.</param>
        /// <exception cref="ArgumentNullException">path is null.</exception>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="System.Text.Json.JsonException">The file is not valid JSON for the schema.</exception>
        public DatasetLoader(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            Path = RepositoryPaths.Resolve(path);
            if (!File.Exists(Path)) throw new FileNotFoundException("Dataset not found: " + Path, Path);
            Sha256 = StableHash.Sha256File(Path);
            Dataset = Json.Deserialize<BenchmarkDataset>(File.ReadAllText(Path));
        }

        #endregion
    }
}
