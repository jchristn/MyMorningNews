namespace MyMorningNews.Core.Email
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Builds the provider from the current settings (read on every send, so changes apply without a restart).
    /// </summary>
    public static class EmailProviderFactory
    {
        /// <summary>
        /// Provider for the settings.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <returns>Provider.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static IEmailProvider Create(EmailSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (!settings.Enabled) return new DisabledEmailProvider();
            switch (settings.Provider)
            {
                case EmailProviderEnum.Mailgun: return new MailgunEmailProvider(settings);
                case EmailProviderEnum.File: return new FileEmailProvider(settings);
                default: return new DisabledEmailProvider();
            }
        }
    }
}
