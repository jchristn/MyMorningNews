namespace MyMorningNews.Core.Email
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Sends nothing; messages are recorded as Skipped.
    /// </summary>
    public class DisabledEmailProvider : IEmailProvider
    {
        /// <inheritdoc />
        public EmailProviderEnum Provider
        {
            get { return EmailProviderEnum.Disabled; }
        }

        /// <inheritdoc />
        public bool IsConfigured
        {
            get { return true; }
        }

        /// <inheritdoc />
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            return Task.FromResult(new EmailSendResult { Error = "Email is disabled.", Permanent = true });
        }
    }
}
