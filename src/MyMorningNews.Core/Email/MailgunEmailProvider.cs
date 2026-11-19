namespace MyMorningNews.Core.Email
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;
    using SendWithMailgun;

    /// <summary>
    /// Sends through Mailgun with SendWithMailgun. The HTML body is sent (the library sends one body format per message).
    /// </summary>
    public class MailgunEmailProvider : IEmailProvider
    {
        private readonly EmailSettings _Settings;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public MailgunEmailProvider(EmailSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public EmailProviderEnum Provider
        {
            get { return EmailProviderEnum.Mailgun; }
        }

        /// <inheritdoc />
        public bool IsConfigured
        {
            get { return !String.IsNullOrWhiteSpace(_Settings.MailgunDomain) && !String.IsNullOrWhiteSpace(_Settings.MailgunApiKey) && !String.IsNullOrWhiteSpace(_Settings.FromAddress); }
        }

        /// <inheritdoc />
        public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (!IsConfigured) return new EmailSendResult { Error = "Mailgun is not configured (domain, API key, and from address).", Permanent = true };
            try
            {
                MailgunSender sender = new MailgunSender(_Settings.MailgunDomain.Trim(), _Settings.MailgunApiKey.Trim(), _Settings.MailgunBaseUrl);
                string from = String.IsNullOrWhiteSpace(_Settings.FromName) ? _Settings.FromAddress : "\"" + _Settings.FromName.Replace("\"", String.Empty) + "\" <" + _Settings.FromAddress + ">";
                string? id = await sender.SendAsync(message.Recipient, from, message.Subject, message.RenderedHtml ?? message.RenderedText ?? String.Empty, !String.IsNullOrEmpty(message.RenderedHtml), null, null, token).ConfigureAwait(false);
                if (String.IsNullOrWhiteSpace(id)) return new EmailSendResult { Error = "Mailgun did not accept the message." };
                return new EmailSendResult { Success = true, ProviderMessageId = id.Trim().Trim('<', '>') };
            }
            catch (HttpRequestException ex)
            {
                return new EmailSendResult { Error = "Could not reach Mailgun: " + ex.Message };
            }
            catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
            {
                return new EmailSendResult { Error = "Mailgun timed out: " + ex.Message };
            }
            catch (ArgumentException ex)
            {
                return new EmailSendResult { Error = ex.Message, Permanent = true };
            }
        }
    }
}
