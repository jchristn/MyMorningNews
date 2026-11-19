namespace MyMorningNews.Server
{
    using System.Threading.Tasks;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static Task<int> Main(string[] args)
        {
            return Bootstrapper.RunAsync(args);
        }
    }
}
