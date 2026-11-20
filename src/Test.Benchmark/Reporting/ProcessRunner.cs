namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Runs a helper process (git, docker, sysctl) with an argument list, never through a shell, and returns its trimmed
    /// standard output. Failures return null so provenance degrades to "unknown" instead of failing the run.
    /// </summary>
    public static class ProcessRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run a process and capture standard output.
        /// </summary>
        /// <param name="fileName">Executable.</param>
        /// <param name="arguments">Arguments, passed individually.</param>
        /// <param name="workingDirectory">Working directory, or null.</param>
        /// <param name="timeoutMs">Timeout in milliseconds. Default 15000.</param>
        /// <returns>Trimmed output, or null when the process failed or could not start.</returns>
        /// <exception cref="ArgumentNullException">fileName or arguments is null.</exception>
        public static string? Run(string fileName, IEnumerable<string> arguments, string? workingDirectory = null, int timeoutMs = 15000)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(arguments);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (string a in arguments) psi.ArgumentList.Add(a);
                if (workingDirectory != null) psi.WorkingDirectory = workingDirectory;
                using (Process? p = Process.Start(psi))
                {
                    if (p == null) return null;
                    string output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try
                        {
                            p.Kill(true);
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        return null;
                    }

                    return p.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is System.IO.IOException)
            {
                return null;
            }
        }

        #endregion
    }
}
