namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Public share links for briefings (plan section 36). A share is created on purpose by someone who can read the
    /// briefing, always expires, can be revoked, and yields a reading link and a listening link that need no sign-in.
    /// Only the SHA-256 hash of the link secret is stored, so the links are shown once.
    /// Thread safety: stateless apart from the database and live settings; safe for concurrent use.
    /// </summary>
    public class BriefingShareService
    {
        #region Public-Members

        /// <summary>
        /// Prefix of every share link secret.
        /// </summary>
        public const string SecretPrefix = "mmns_";

        /// <summary>
        /// Whether public sharing is turned on (sharing.enabled).
        /// </summary>
        public bool Enabled
        {
            get { return _Settings.Sharing.Enabled; }
        }

        /// <summary>
        /// Shortest life of a share link in days. Always 1.
        /// </summary>
        public int MinExpiryDays
        {
            get { return 1; }
        }

        /// <summary>
        /// Longest life of a share link in days (sharing.maxExpiryDays). Default 14.
        /// </summary>
        public int MaxExpiryDays
        {
            get { return _Settings.Sharing.MaxExpiryDays; }
        }

        /// <summary>
        /// Life of a share link in days when the request does not say: sharing.defaultExpiryDays, never more than <see cref="MaxExpiryDays"/>. Default 7.
        /// </summary>
        public int DefaultExpiryDays
        {
            get { return Math.Min(_Settings.Sharing.DefaultExpiryDays, _Settings.Sharing.MaxExpiryDays); }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Settings (read live).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public BriefingShareService(DatabaseDriverBase database, ServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(settings);
            _Database = database;
            _Settings = settings;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a share for a briefing. The secret and both links are returned once; only a hash is stored.
        /// </summary>
        /// <param name="briefing">Briefing to share. Must be ready.</param>
        /// <param name="userId">The user creating the share.</param>
        /// <param name="expiresInDays">Days until the links stop working, from <see cref="MinExpiryDays"/> to <see cref="MaxExpiryDays"/>; null uses <see cref="DefaultExpiryDays"/>.</param>
        /// <param name="showOwnerName">Show the user's display name on the public pages.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The share with its secret and links.</returns>
        /// <exception cref="ArgumentNullException">briefing is null or userId is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">expiresInDays is outside the allowed range.</exception>
        /// <exception cref="InvalidOperationException">Sharing is turned off, or the briefing is not ready.</exception>
        public async Task<BriefingShareCreated> CreateAsync(Briefing briefing, string userId, int? expiresInDays, bool showOwnerName, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            if (!Enabled) throw new InvalidOperationException("Public sharing is turned off on this server.");
            if (briefing.Status != BriefingStatusEnum.Ready) throw new InvalidOperationException("Only a ready briefing can be shared.");
            int days = expiresInDays ?? DefaultExpiryDays;
            if (days < MinExpiryDays || days > MaxExpiryDays)
                throw new ArgumentOutOfRangeException(nameof(expiresInDays), "expiresInDays must be between " + MinExpiryDays + " and " + MaxExpiryDays + ".");

            string secret = SecretPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(30)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            BriefingShare share = await _Database.BriefingShares.CreateAsync(new BriefingShare
            {
                TenantId = briefing.TenantId,
                UserId = userId,
                BriefingId = briefing.Id,
                TokenHash = Hash(secret),
                TokenPrefix = secret.Substring(0, 10),
                ShowOwnerName = showOwnerName,
                ExpiresUtc = DateTime.UtcNow.AddDays(days)
            }, token).ConfigureAwait(false);

            return new BriefingShareCreated
            {
                Share = share,
                Secret = secret,
                Url = ReadUrl(secret),
                ReadUrl = ReadUrl(secret),
                ListenUrl = ReadUrl(secret),
                AudioReady = await ReadyAudioAsync(briefing, token).ConfigureAwait(false) != null
            };
        }

        /// <summary>
        /// Whether a share still works at a given time: active, not revoked, and not expired.
        /// </summary>
        /// <param name="share">Share.</param>
        /// <param name="nowUtc">Time to check against.</param>
        /// <returns>True when live.</returns>
        /// <exception cref="ArgumentNullException">share is null.</exception>
        public static bool IsLive(BriefingShare share, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(share);
            return share.Active && !share.RevokedUtc.HasValue && share.ExpiresUtc > nowUtc;
        }

        /// <summary>
        /// Resolve a link secret to what its public pages show. Returns null when sharing is off, the secret is
        /// unknown, the share is revoked or expired, the briefing is gone or not ready, or the sharing user or tenant
        /// is gone, inactive, or pending deletion. The caller answers every null the same way.
        /// </summary>
        /// <param name="secret">Secret from the URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Shared briefing, or null.</returns>
        public async Task<SharedBriefing?> ResolveAsync(string? secret, CancellationToken token = default)
        {
            if (!Enabled) return null;
            if (String.IsNullOrWhiteSpace(secret) || secret.Length > 128 || !secret.StartsWith(SecretPrefix, StringComparison.Ordinal)) return null;
            BriefingShare? share = await _Database.BriefingShares.ReadByHashAsync(Hash(secret), token).ConfigureAwait(false);
            if (share == null || !IsLive(share, DateTime.UtcNow)) return null;
            Tenant? tenant = await _Database.Tenants.ReadAsync(share.TenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return null;
            User? owner = await _Database.Users.ReadAsync(share.TenantId, share.UserId, token).ConfigureAwait(false);
            if (owner == null || !owner.Active || owner.DeletionRequestedUtc.HasValue) return null;
            Briefing? briefing = await _Database.Briefings.ReadAsync(share.TenantId, share.BriefingId, token).ConfigureAwait(false);
            if (briefing == null || briefing.Status != BriefingStatusEnum.Ready) return null;

            SharedBriefing shared = new SharedBriefing { Share = share, Briefing = briefing };
            if (briefing.TopicId != null) shared.TopicName = (await _Database.Topics.ReadAsync(briefing.TenantId, briefing.TopicId, token).ConfigureAwait(false))?.Name;
            if (shared.TopicName == null && briefing.EditionId != null) shared.TopicName = (await _Database.Editions.ReadAsync(briefing.TenantId, briefing.EditionId, token).ConfigureAwait(false))?.Name;
            if (share.ShowOwnerName)
            {
                string name = ((owner.FirstName ?? String.Empty).Trim() + " " + (owner.LastName ?? String.Empty).Trim()).Trim();
                shared.OwnerName = name.Length == 0 ? null : name;
            }

            MediaAsset? audio = await ReadyAudioAsync(briefing, token).ConfigureAwait(false);
            if (audio != null && audio.ArtifactId != null)
            {
                Artifact? file = await _Database.Artifacts.ReadAsync(audio.TenantId, audio.ArtifactId, token).ConfigureAwait(false);
                if (file != null && !file.DeletedUtc.HasValue)
                {
                    shared.Audio = audio;
                    shared.AudioFile = file;
                    shared.Chapters = (await _Database.MediaChapters.EnumerateAsync(new MediaChapterFilter { TenantId = audio.TenantId, MediaAssetId = audio.Id, PageSize = 200 }, token).ConfigureAwait(false)).Objects
                        .OrderBy(c => c.SortOrder).ToList();
                }
            }

            return shared;
        }

        /// <summary>
        /// Count one opening of a public page.
        /// </summary>
        /// <param name="share">Share.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">share is null.</exception>
        public async Task RecordViewAsync(BriefingShare share, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(share);
            await _Database.BriefingShares.RecordViewAsync(share.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Shares of one briefing, newest first, with the hash removed.
        /// </summary>
        /// <param name="briefing">Briefing.</param>
        /// <param name="userId">Limit to shares created by this user, or null for every share of the briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Shares.</returns>
        /// <exception cref="ArgumentNullException">briefing is null.</exception>
        public async Task<List<BriefingShare>> ListAsync(Briefing briefing, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            List<BriefingShare> shares = (await _Database.BriefingShares.EnumerateAsync(new BriefingShareFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, UserId = userId, PageSize = 200 }, token).ConfigureAwait(false)).Objects;
            foreach (BriefingShare s in shares) s.TokenHash = "redacted";
            return shares;
        }

        /// <summary>
        /// Public page URL for a secret: the single link for listening and reading.
        /// </summary>
        /// <param name="secret">Link secret.</param>
        /// <returns>Absolute URL.</returns>
        public string ReadUrl(string secret)
        {
            return _Settings.Rest.PublicBaseUrl + PagePath(secret);
        }

        /// <summary>
        /// Server path of the reading page for a secret (no host).
        /// </summary>
        /// <param name="secret">Link secret.</param>
        /// <returns>Path starting with /v1.0/public/shares/.</returns>
        public static string PagePath(string secret)
        {
            return "/v1.0/public/shares/" + Uri.EscapeDataString(secret ?? String.Empty);
        }

        /// <summary>
        /// File name used in the public audio URL: "briefing" plus the stored file's extension (.wav when unknown).
        /// </summary>
        /// <param name="file">Stored audio file.</param>
        /// <returns>File name such as briefing.wav.</returns>
        /// <exception cref="ArgumentNullException">file is null.</exception>
        public static string AudioFileName(Artifact file)
        {
            ArgumentNullException.ThrowIfNull(file);
            string ext = Path.GetExtension(file.FileName ?? String.Empty).ToLowerInvariant();
            if (ext.Length < 2 || ext.Length > 6 || !ext.Skip(1).All(Char.IsLetterOrDigit)) ext = ".wav";
            return "briefing" + ext;
        }

        /// <summary>
        /// SHA-256 of a secret, lowercase hex.
        /// </summary>
        /// <param name="secret">Secret.</param>
        /// <returns>Hash.</returns>
        public static string Hash(string secret)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? String.Empty))).ToLowerInvariant();
        }

        #endregion

        #region Private-Methods

        private async Task<MediaAsset?> ReadyAudioAsync(Briefing briefing, CancellationToken token)
        {
            List<MediaAsset> all = (await _Database.MediaAssets.EnumerateAsync(new MediaAssetFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, Status = MediaStatusEnum.Ready, PageSize = 20 }, token).ConfigureAwait(false)).Objects;
            return all.Where(m => m.ArtifactId != null).OrderByDescending(m => m.Id == briefing.MediaAssetId).ThenByDescending(m => m.CreatedUtc).FirstOrDefault();
        }

        #endregion
    }
}
