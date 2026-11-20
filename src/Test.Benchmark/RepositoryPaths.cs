namespace MyMorningNews.Benchmark
{
    using System;
    using System.IO;

    /// <summary>
    /// Default paths, resolved from the repository root: the first folder, walking up from the executable and then from
    /// the working directory, that contains both .git and benchmarks/. Never resolved from the working directory alone.
    /// </summary>
    public static class RepositoryPaths
    {
        #region Public-Methods

        /// <summary>
        /// The repository root.
        /// </summary>
        /// <returns>Absolute path.</returns>
        /// <exception cref="DirectoryNotFoundException">No folder containing .git and benchmarks/ was found.</exception>
        public static string Root()
        {
            string? found = Walk(AppContext.BaseDirectory) ?? Walk(Directory.GetCurrentDirectory());
            if (found == null) throw new DirectoryNotFoundException("Could not find the repository root (a folder with .git and benchmarks/) above " + AppContext.BaseDirectory + ".");
            return found;
        }

        /// <summary>
        /// benchmarks/ under the root.
        /// </summary>
        /// <returns>Absolute path.</returns>
        public static string Benchmarks()
        {
            return Path.Combine(Root(), "benchmarks");
        }

        /// <summary>
        /// benchmarks/results/, the default report directory.
        /// </summary>
        /// <returns>Absolute path.</returns>
        public static string Results()
        {
            return Path.Combine(Benchmarks(), "results");
        }

        /// <summary>
        /// benchmarks/.run/, the git-ignored scratch directory.
        /// </summary>
        /// <returns>Absolute path.</returns>
        public static string Run()
        {
            return Path.Combine(Benchmarks(), ".run");
        }

        /// <summary>
        /// Resolve a path given on the command line: absolute paths are kept, relative paths resolve from the root.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <returns>Absolute path.</returns>
        /// <exception cref="ArgumentNullException">path is null.</exception>
        public static string Resolve(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (Path.IsPathRooted(path)) return path;
            string fromCwd = Path.GetFullPath(path);
            if (File.Exists(fromCwd) || Directory.Exists(fromCwd)) return fromCwd;
            return Path.GetFullPath(Path.Combine(Root(), path));
        }

        #endregion

        #region Private-Methods

        private static string? Walk(string start)
        {
            DirectoryInfo? dir = new DirectoryInfo(start);
            while (dir != null)
            {
                bool git = Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"));
                if (git && Directory.Exists(Path.Combine(dir.FullName, "benchmarks"))) return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }

        #endregion
    }
}
