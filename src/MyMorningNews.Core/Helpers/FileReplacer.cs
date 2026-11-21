namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Replaces a file's contents. Writes a temporary file beside the target and renames it over the target, so readers
    /// never see a partial file. When the rename cannot be done, for example because the target is a single-file Docker
    /// bind mount (the rename fails with "Device or resource busy") or the directory is not writable, the contents are
    /// written to the target in place instead.
    /// Thread safety: callers serialize writes to the same path.
    /// </summary>
    public static class FileReplacer
    {
        /// <summary>
        /// Replace a file's contents with text.
        /// </summary>
        /// <param name="path">Target file path.</param>
        /// <param name="contents">New contents.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the file was replaced atomically by a rename, false when it was written in place.</returns>
        /// <exception cref="ArgumentNullException">path or contents is null.</exception>
        /// <exception cref="IOException">The file could not be written either way.</exception>
        /// <exception cref="UnauthorizedAccessException">The file itself is not writable.</exception>
        public static async Task<bool> WriteAllTextAsync(string path, string contents, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(contents);
            token.ThrowIfCancellationRequested();
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temp, contents, token).ConfigureAwait(false);
                File.Move(temp, path, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temp);
            }

            await File.WriteAllTextAsync(path, contents, token).ConfigureAwait(false);
            return false;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}
