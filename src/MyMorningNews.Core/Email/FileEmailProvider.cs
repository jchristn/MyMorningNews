namespace MyMorningNews.Core.Email
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Writes each email as {id}.html, {id}.txt, and {id}.json to a directory, so the loop works before Mailgun is set up
    /// and messages can be opened locally.
    /// </summary>
    public class FileEmailProvider : IEmailProvider
    {
        private readonly EmailSettings _Settings;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public FileEmailProvider(EmailSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public EmailProviderEnum Provider
        {
            get { return EmailProviderEnum.File; }
        }

        /// <inheritdoc />
        public bool IsConfigured
        {
            get { return !String.IsNullOrWhiteSpace(_Settings.FileDirectory); }
        }

        /// <inheritdoc />
        public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            try
            {
                string dir = Path.GetFullPath(_Settings.FileDirectory);
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, message.Id + ".html"), message.RenderedHtml ?? String.Empty, Encoding.UTF8, token).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(dir, message.Id + ".txt"), message.RenderedText ?? String.Empty, Encoding.UTF8, token).ConfigureAwait(false);
                string meta = JsonSerializer.Serialize(new { id = message.Id, to = message.Recipient, from = _Settings.FromAddress, subject = message.Subject, template = message.Template.ToString(), createdUtc = message.CreatedUtc });
                await File.WriteAllTextAsync(Path.Combine(dir, message.Id + ".json"), meta, Encoding.UTF8, token).ConfigureAwait(false);
                return new EmailSendResult { Success = true, ProviderMessageId = "file:" + message.Id };
            }
            catch (IOException ex)
            {
                return new EmailSendResult { Error = "Could not write the email file: " + ex.Message };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new EmailSendResult { Error = "Could not write the email file: " + ex.Message, Permanent = true };
            }
        }
    }
}
