namespace MyMorningNews.Core.Email
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Sends a rendered email (plan section 23.1).
    /// </summary>
    public interface IEmailProvider
    {
        /// <summary>
        /// Provider.
        /// </summary>
        EmailProviderEnum Provider { get; }

        /// <summary>
        /// Whether the provider has what it needs to send.
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Send. Never throws for delivery failures; they are returned.
        /// </summary>
        /// <param name="message">Rendered message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome.</returns>
        Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default);
    }
}
