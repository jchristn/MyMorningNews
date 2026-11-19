namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console test runner. Accepts --provider postgresql (stripped before Touchstone parses the remaining arguments).
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            List<string> remaining = new List<string>();
            string? provider = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--provider", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length) return ConsoleRunner.ExitInvalidArguments;
                    provider = args[i + 1];
                    i++;
                    continue;
                }

                remaining.Add(args[i]);
            }

            if (!TestEnvironment.IsSupportedProvider(provider))
            {
                Console.Error.WriteLine("A supported --provider is required. Supported: postgresql.");
                return ConsoleRunner.ExitInvalidArguments;
            }

            TestEnvironment.Provider = provider!;
            return await ConsoleRunner.RunMainAsync(MyMorningNewsSuites.All, remaining.ToArray()).ConfigureAwait(false);
        }
    }
}
