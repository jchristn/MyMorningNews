namespace MyMorningNews.McpServer
{
    using System;

    /// <summary>
    /// MCP server entry point. The tool surface is implemented in Phase 11 (see MY_MORNING_NEWS_PLAN.md section 24.2).
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static int Main(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Console.Error.WriteLine("MyMorningNews.McpServer is not implemented yet.");
            return 1;
        }
    }
}
