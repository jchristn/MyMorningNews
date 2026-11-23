namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Audio for briefings (plan section 22): queue, write the spoken script (podcast.script, or from the briefing text
    /// when no model is available), split into segments, synthesize with bounded parallelism and retries, join and
    /// normalize, store the audio and chapters, and build private podcast feeds. Thread safety: safe for concurrent use.
    /// </summary>
    public class AudioService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Name of the seeded voice profile.
        /// </summary>
        public const string DefaultVoiceName = "Kokoro (local)";

        /// <summary>
        /// Prefix of feed token secrets.
        /// </summary>
        public const string FeedTokenPrefix = "mmnf_";

        /// <summary>
        /// Mixer.
        /// </summary>
        public AudioMixer Mixer { get; }

        /// <summary>
        /// Set whenever an episode is queued through this service. The media worker waits on it between passes, with its
        /// poll interval as the timeout. Never null; owned and disposed by this service.
        /// </summary>
        public WorkSignal QueueSignal { get; } = new WorkSignal();

        /// <summary>
        /// Notifications; when set, the owner is told once when a briefing's audio becomes ready (not for dry runs,
        /// voice previews, or voice tests, which make no stored audio). An edition's email and push switches are passed
        /// as further filters on top of the reader's notification preferences. May be null.
        /// </summary>
        public NotificationService? Notifications { get; set; } = null;

        /// <summary>
        /// Seconds the speech server's voice list is kept before it is read again. Default 300, minimum 0 (never
        /// kept), maximum 86400.
        /// </summary>
        public int VoiceListCacheSeconds
        {
            get { return _VoiceListCacheSeconds; }
            set { _VoiceListCacheSeconds = Math.Clamp(value, 0, 86400); }
        }

        /// <summary>
        /// Seconds to wait for the speech server to list its voices. Default 5, minimum 1, maximum 60.
        /// </summary>
        public int VoiceListTimeoutSeconds
        {
            get { return _VoiceListTimeoutSeconds; }
            set { _VoiceListTimeoutSeconds = Math.Clamp(value, 1, 60); }
        }

        /// <summary>
        /// Most characters spoken by a voice preview. Default 300, minimum 20, maximum 1200.
        /// </summary>
        public int PreviewMaxCharacters
        {
            get { return _PreviewMaxCharacters; }
            set { _PreviewMaxCharacters = Math.Clamp(value, 20, 1200); }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ModelGateway _Models;
        private readonly PromptService _Prompts;
        private readonly ArtifactService _Artifacts;
        private readonly VaultService _Vault;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ITtsProvider _Tts;
        private readonly bool _OwnsTts;
        private readonly HttpClient _Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private readonly object _VoiceListLock = new object();
        private int _VoiceListCacheSeconds = 300;
        private int _VoiceListTimeoutSeconds = 5;
        private int _PreviewMaxCharacters = 300;
        private string? _VoiceListEndpoint = null;
        private List<string>? _VoiceList = null;
        private DateTime _VoiceListUtc = DateTime.MinValue;
        private bool _Disposed = false;
        private const string _Header = "[AudioService] ";
        private const int _ClaimBatch = 2;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="artifacts">Artifact storage.</param>
        /// <param name="vault">Vault (speech API keys).</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <param name="tts">Speech provider; null uses the OpenAI-compatible provider.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AudioService(DatabaseDriverBase database, ModelGateway models, PromptService prompts, ArtifactService artifacts, VaultService vault, ServerSettings settings, LoggingModule logging, ITtsProvider? tts = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
            _Vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _OwnsTts = tts == null;
            _Tts = tts ?? new OpenAiCompatibleSpeechProvider();
            Mixer = new AudioMixer(settings.Tts);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the default voice profile (the bundled Kokoro server) when the tenant has none.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when created.</returns>
        public async Task<bool> EnsureDefaultVoiceAsync(string tenantId, CancellationToken token = default)
        {
            if (await _Database.VoiceProfiles.CountAsync(new VoiceProfileFilter { TenantId = tenantId }, token).ConfigureAwait(false) > 0) return false;
            await _Database.VoiceProfiles.CreateAsync(new VoiceProfile
            {
                TenantId = tenantId,
                Name = DefaultVoiceName,
                Provider = TtsProviderEnum.OpenAiCompatibleSpeech,
                Model = _Settings.Tts.Model,
                Voice = _Settings.Tts.Voice,
                SecondaryVoice = _Settings.Tts.SecondaryVoice,
                IsDefault = true
            }, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Queue audio for a briefing. Returns the existing episode unless force is set or it failed.
        /// </summary>
        /// <param name="briefing">Ready briefing.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="voiceProfileId">Voice, or null for the edition's voice, then the topic's voice (a topic's own briefing), then the tenant default.</param>
        /// <param name="force">Make a new episode even when one exists.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The queued or existing episode, or null when audio is turned off.</returns>
        /// <exception cref="ConflictException">The briefing is not ready.</exception>
        public async Task<MediaAsset?> QueueForBriefingAsync(Briefing briefing, MediaKindEnum kind, string? voiceProfileId, bool force, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            if (!_Settings.Tts.Enabled) return null;
            if (briefing.Status != BriefingStatusEnum.Ready || String.IsNullOrWhiteSpace(briefing.Markdown)) throw new ConflictException("Audio is made from a ready briefing.");
            List<MediaAsset> existing = (await _Database.MediaAssets.EnumerateAsync(new MediaAssetFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 20 }, token).ConfigureAwait(false)).Items;
            MediaAsset? live = existing.Where(m => m.Status != MediaStatusEnum.Failed).OrderByDescending(m => m.CreatedUtc).FirstOrDefault();
            if (live != null && !force) return live;
            if (live != null && live.Status != MediaStatusEnum.Ready) throw new ConflictException("Audio for this briefing is already being made.");
            BriefingTemplateDefinition template = await TemplateAsync(briefing, token).ConfigureAwait(false);
            Edition? edition = briefing.EditionId == null ? null : await _Database.Editions.ReadAsync(briefing.TenantId, briefing.EditionId, token).ConfigureAwait(false);
            string? chosen = String.IsNullOrEmpty(voiceProfileId) ? edition?.VoiceProfileId : voiceProfileId;
            if (String.IsNullOrEmpty(chosen) && briefing.EditionId == null && !String.IsNullOrEmpty(briefing.TopicId))
            {
                // A topic's own briefing is read in the voice chosen for that topic.
                Topic? topic = await _Database.Topics.ReadAsync(briefing.TenantId, briefing.TopicId, token).ConfigureAwait(false);
                chosen = topic?.VoiceProfileId;
            }

            VoiceProfile? voice = await ResolveVoiceAsync(briefing.TenantId, chosen, token).ConfigureAwait(false);
            MediaAsset queued = await _Database.MediaAssets.CreateAsync(new MediaAsset
            {
                TenantId = briefing.TenantId,
                UserId = briefing.UserId,
                BriefingId = briefing.Id,
                EditionId = briefing.EditionId,
                Kind = kind,
                Status = MediaStatusEnum.Queued,
                Format = template.Audio.Format,
                VoiceProfileId = voice?.Id,
                Title = briefing.Title
            }, token).ConfigureAwait(false);
            QueueSignal.Signal();
            return queued;
        }

        /// <summary>
        /// One worker pass: recover abandoned claims, then produce claimed episodes.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Episodes finished or failed in this pass.</returns>
        public Task<int> ProcessQueuedAsync(DateTime nowUtc, CancellationToken token = default)
        {
            return ProcessQueuedAsync(nowUtc, null, token);
        }

        /// <summary>
        /// One worker pass limited to some tenants (or any tenant).
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="tenantIds">Only these tenants, or null for any tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Episodes finished or failed in this pass.</returns>
        public async Task<int> ProcessQueuedAsync(DateTime nowUtc, IReadOnlyCollection<string>? tenantIds, CancellationToken token = default)
        {
            await _Database.MediaAssets.RecoverAbandonedAsync(nowUtc, _Settings.Tts.MaxAttempts, token).ConfigureAwait(false);
            List<MediaAsset> claimed = await _Database.MediaAssets.ClaimQueuedAsync(nowUtc, nowUtc.AddMinutes(30), _ClaimBatch, tenantIds, token).ConfigureAwait(false);
            int done = 0;
            bool retrying = false;
            foreach (MediaAsset m in claimed)
            {
                token.ThrowIfCancellationRequested();
                if (!await ProduceAsync(m, token).ConfigureAwait(false)) retrying = true;
                done++;
            }

            // A full batch means more episodes may be waiting: ask for another pass at once rather than at the next
            // poll. Not when an episode went back to the queue to retry, so retries keep the poll interval as their pause.
            if (claimed.Count == _ClaimBatch && !retrying) QueueSignal.Signal();

            return done;
        }

        /// <summary>
        /// Chapters of an episode in order.
        /// </summary>
        /// <param name="asset">Episode.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Chapters.</returns>
        public async Task<List<MediaChapter>> ChaptersAsync(MediaAsset asset, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            return (await _Database.MediaChapters.EnumerateAsync(new MediaChapterFilter { TenantId = asset.TenantId, MediaAssetId = asset.Id, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .OrderBy(c => c.SortOrder).ToList();
        }

        /// <summary>
        /// Delete an episode with its files and chapters, and unlink it from its briefing.
        /// </summary>
        /// <param name="asset">Episode.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(MediaAsset asset, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            foreach (string? id in new[] { asset.ArtifactId, asset.ChaptersArtifactId })
            {
                if (id == null) continue;
                Artifact? a = await _Database.Artifacts.ReadAsync(asset.TenantId, id, token).ConfigureAwait(false);
                if (a == null) continue;
                await _Artifacts.DeleteContentAsync(a, token).ConfigureAwait(false);
                await _Database.Artifacts.DeleteAsync(a.TenantId, a.Id, token).ConfigureAwait(false);
            }

            foreach (MediaChapter c in await ChaptersAsync(asset, token).ConfigureAwait(false)) await _Database.MediaChapters.DeleteAsync(c.TenantId, c.Id, token).ConfigureAwait(false);
            Briefing? b = await _Database.Briefings.ReadAsync(asset.TenantId, asset.BriefingId, token).ConfigureAwait(false);
            if (b != null && b.MediaAssetId == asset.Id)
            {
                b.MediaAssetId = null;
                await _Database.Briefings.UpdateAsync(b, token).ConfigureAwait(false);
            }

            await _Database.MediaAssets.DeleteAsync(asset.TenantId, asset.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Speak a sample with a voice (Voices page test button).
        /// </summary>
        /// <param name="voice">Voice profile.</param>
        /// <param name="text">Text (up to MaxSegmentCharacters).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audio.</returns>
        /// <exception cref="TtsException">The provider failed.</exception>
        public async Task<TtsResult> SampleAsync(VoiceProfile voice, string text, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(voice);
            if (voice.Provider == TtsProviderEnum.Disabled) throw new ConflictException("This voice is disabled.");
            string t = SpeechScript.Clean(text);
            if (t.Length == 0) t = "Good morning. Here is what moved in your topics.";
            if (t.Length > _Settings.Tts.MaxSegmentCharacters) t = t.Substring(0, _Settings.Tts.MaxSegmentCharacters);
            return await _Tts.SynthesizeAsync(await RequestAsync(voice, voice.Voice, t, token).ConfigureAwait(false), token).ConfigureAwait(false);
        }

        /// <summary>
        /// The voices someone in a workspace can choose for a topic: the workspace's voice profiles, then the voices
        /// the speech server offers that have no profile yet. The server's list comes from GET {endpoint}/audio/voices
        /// (Kokoro-FastAPI) and is kept for <see cref="VoiceListCacheSeconds"/>; when the server has no listing, the
        /// configured default voices are offered instead.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Voices. Never null.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<AvailableVoicesResponse> AvailableVoicesAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            AvailableVoicesResponse response = new AvailableVoicesResponse { Enabled = _Settings.Tts.Enabled };
            List<VoiceProfile> profiles = await ProfilesAsync(tenantId, token).ConfigureAwait(false);
            VoiceProfile? fallback = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.OrderBy(p => p.CreatedUtc).FirstOrDefault();
            response.DefaultVoiceProfileId = fallback?.Id;
            foreach (VoiceProfile p in profiles.Where(p => p.Active && p.Provider != TtsProviderEnum.Disabled)
                .OrderByDescending(p => fallback != null && p.Id == fallback.Id).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                response.Voices.Add(new AvailableVoice
                {
                    Voice = p.Voice,
                    Label = VoiceNames.Describe(p.Voice),
                    VoiceProfileId = p.Id,
                    Name = p.Name,
                    IsDefault = fallback != null && p.Id == fallback.Id
                });
            }

            List<string>? listed = await ListedVoicesAsync(token).ConfigureAwait(false);
            response.Listed = listed != null;
            foreach (string voice in listed ?? ConfiguredVoices())
            {
                if (profiles.Any(p => IsServerProfile(p) && String.Equals(p.Voice, voice, StringComparison.Ordinal))) continue;
                response.Voices.Add(new AvailableVoice { Voice = voice, Label = VoiceNames.Describe(voice) });
            }

            return response;
        }

        /// <summary>
        /// Speak a short sample so someone can hear a voice before choosing it. With a voice profile, its endpoint,
        /// model, voice, and speed are used; otherwise the voice must be one the speech server offers.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="req">Voice or voice profile, and optional text (cut to <see cref="PreviewMaxCharacters"/>).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audio.</returns>
        /// <exception cref="ArgumentNullException">tenantId or req is null.</exception>
        /// <exception cref="ArgumentException">Neither a voice nor a voice profile was given, or the speech server does not offer the voice.</exception>
        /// <exception cref="KeyNotFoundException">The voice profile does not exist in the tenant.</exception>
        /// <exception cref="ConflictException">Speech is turned off, or the voice profile is disabled.</exception>
        /// <exception cref="TtsException">The speech server failed.</exception>
        public async Task<TtsResult> PreviewAsync(string tenantId, VoicePreviewRequest req, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ArgumentNullException.ThrowIfNull(req);
            if (String.IsNullOrWhiteSpace(req.VoiceProfileId) && String.IsNullOrWhiteSpace(req.Voice)) throw new ArgumentException("voice or voiceProfileId is required.");
            if (!_Settings.Tts.Enabled) throw new ConflictException("Speech is turned off on this server, so there is nothing to play.");
            string text = SpeechScript.Clean(req.Text ?? String.Empty);
            if (text.Length > _PreviewMaxCharacters) text = text.Substring(0, _PreviewMaxCharacters);

            VoiceProfile profile;
            if (!String.IsNullOrWhiteSpace(req.VoiceProfileId))
            {
                profile = await _Database.VoiceProfiles.ReadAsync(tenantId, req.VoiceProfileId.Trim(), token).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException("Voice profile " + req.VoiceProfileId.Trim() + " was not found.");
            }
            else
            {
                string voice = (req.Voice ?? String.Empty).Trim();
                await RequireOfferedAsync(voice, token).ConfigureAwait(false);
                profile = new VoiceProfile { TenantId = tenantId, Name = voice, Model = _Settings.Tts.Model, Voice = voice };
            }

            return await SampleAsync(profile, text, token).ConfigureAwait(false);
        }

        /// <summary>
        /// The workspace voice profile for a speech server voice: the existing profile that uses the voice on the
        /// server's default speech endpoint, or a new one named after the voice.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="voice">Speech server voice ID, such as af_heart.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Voice profile.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="ArgumentException">The voice is empty, or the speech server does not offer it.</exception>
        public async Task<VoiceProfile> EnsureProfileForVoiceAsync(string tenantId, string voice, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            string id = (voice ?? String.Empty).Trim();
            if (id.Length == 0) throw new ArgumentException("voice is required.");
            List<VoiceProfile> profiles = await ProfilesAsync(tenantId, token).ConfigureAwait(false);
            VoiceProfile? existing = profiles.Where(p => p.Active && IsServerProfile(p) && String.Equals(p.Voice, id, StringComparison.Ordinal))
                .OrderByDescending(p => p.IsDefault).ThenBy(p => p.CreatedUtc).FirstOrDefault();
            if (existing != null) return existing;
            await RequireOfferedAsync(id, token).ConfigureAwait(false);

            string label = VoiceNames.Describe(id);
            string name = label;
            for (int i = 2; profiles.Any(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); i++) name = label + " " + i;
            string other = String.Equals(_Settings.Tts.SecondaryVoice, id, StringComparison.Ordinal) ? _Settings.Tts.Voice : _Settings.Tts.SecondaryVoice;
            VoiceProfile created = await _Database.VoiceProfiles.CreateAsync(new VoiceProfile
            {
                TenantId = tenantId,
                Name = name,
                Provider = TtsProviderEnum.OpenAiCompatibleSpeech,
                Model = _Settings.Tts.Model,
                Voice = id,
                SecondaryVoice = String.IsNullOrWhiteSpace(other) || String.Equals(other, id, StringComparison.Ordinal) ? null : other,
                IsDefault = false
            }, token).ConfigureAwait(false);
            _Logging.Info(_Header + "created voice profile " + created.Id + " for voice " + id + " in tenant " + tenantId);
            return created;
        }

        /// <summary>
        /// Check the configured speech endpoint with one very short synthesis in the default voice.
        /// </summary>
        /// <param name="timeoutSeconds">Seconds to wait, 1 to 120.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Null when the endpoint returned audio; otherwise what went wrong.</returns>
        public async Task<string?> ProbeAsync(int timeoutSeconds, CancellationToken token = default)
        {
            try
            {
                await _Tts.SynthesizeAsync(new TtsRequest
                {
                    Endpoint = _Settings.Tts.Endpoint,
                    Model = _Settings.Tts.Model,
                    Voice = _Settings.Tts.Voice,
                    Format = "wav",
                    Text = "Ready.",
                    TimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120)
                }, token).ConfigureAwait(false);
                return null;
            }
            catch (TtsException ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Create a private feed token. Only its hash is stored; the secret is returned once.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="editionId">Limit to one edition, or null.</param>
        /// <param name="name">Label.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token with its secret and feed URL.</returns>
        public async Task<FeedTokenCreated> CreateFeedTokenAsync(string tenantId, string userId, string? editionId, string name, CancellationToken token = default)
        {
            string secret = FeedTokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(30)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            FeedToken ft = await _Database.FeedTokens.CreateAsync(new FeedToken
            {
                TenantId = tenantId,
                UserId = userId,
                EditionId = editionId,
                Name = String.IsNullOrWhiteSpace(name) ? "Podcast app" : name.Trim(),
                TokenHash = Hash(secret),
                TokenPrefix = secret.Substring(0, 10)
            }, token).ConfigureAwait(false);
            return new FeedTokenCreated { Token = ft, Secret = secret, FeedUrl = FeedUrl(secret) };
        }

        /// <summary>
        /// Resolve a feed token secret to a live token and record its use.
        /// </summary>
        /// <param name="secret">Secret from the URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token, or null when unknown or revoked.</returns>
        public async Task<FeedToken?> AuthenticateFeedAsync(string? secret, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(secret) || !secret.StartsWith(FeedTokenPrefix, StringComparison.Ordinal)) return null;
            FeedToken? ft = await _Database.FeedTokens.ReadByHashAsync(Hash(secret), token).ConfigureAwait(false);
            if (ft == null || ft.RevokedUtc.HasValue || !ft.Active) return null;
            if (!ft.LastUsedUtc.HasValue || ft.LastUsedUtc.Value < DateTime.UtcNow.AddMinutes(-5))
            {
                ft.LastUsedUtc = DateTime.UtcNow;
                await _Database.FeedTokens.UpdateAsync(ft, token).ConfigureAwait(false);
            }

            return ft;
        }

        /// <summary>
        /// Episodes a feed token can see: ready audio for its owner (or its edition), newest first.
        /// </summary>
        /// <param name="ft">Feed token.</param>
        /// <param name="max">Most episodes (1 to 500).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Episodes.</returns>
        public async Task<List<MediaAsset>> FeedEpisodesAsync(FeedToken ft, int max, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ft);
            MediaAssetFilter filter = new MediaAssetFilter { TenantId = ft.TenantId, UserId = ft.UserId, EditionId = ft.EditionId, Status = MediaStatusEnum.Ready, PageSize = Math.Clamp(max, 1, 500) };
            return (await _Database.MediaAssets.EnumerateAsync(filter, token).ConfigureAwait(false)).Items
                .Where(m => m.ArtifactId != null).OrderByDescending(m => m.CompletedUtc ?? m.CreatedUtc).ToList();
        }

        /// <summary>
        /// The podcast RSS 2.0 document (with iTunes tags) for a feed token.
        /// </summary>
        /// <param name="ft">Feed token.</param>
        /// <param name="secret">The token secret (used in enclosure URLs).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>XML.</returns>
        public async Task<string> BuildFeedAsync(FeedToken ft, string secret, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ft);
            XNamespace itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
            XNamespace podcast = "https://podcastindex.org/namespace/1.0";
            User? owner = await _Database.Users.ReadAsync(ft.TenantId, ft.UserId, token).ConfigureAwait(false);
            Edition? edition = ft.EditionId == null ? null : await _Database.Editions.ReadAsync(ft.TenantId, ft.EditionId, token).ConfigureAwait(false);
            string who = owner == null ? String.Empty : ((owner.FirstName ?? String.Empty) + " " + (owner.LastName ?? String.Empty)).Trim();
            string title = "My Morning News" + (edition != null ? ": " + edition.Name : (who.Length > 0 ? " for " + who : String.Empty));
            string baseUrl = _Settings.Rest.PublicBaseUrl;
            string artwork = baseUrl + "/v1.0/public/assets/glyph.png";
            XElement channel = new XElement("channel",
                new XElement("title", title),
                new XElement("link", _Settings.Rest.AppBaseUrl),
                new XElement("description", "Your private briefings from My Morning News. This feed is personal; do not share its address."),
                new XElement("language", "en"),
                new XElement("copyright", BriefingHtmlRenderer.Copyright),
                new XElement(itunes + "author", "My Morning News"),
                new XElement(itunes + "summary", "Your private briefings from My Morning News."),
                new XElement(itunes + "explicit", "false"),
                new XElement(itunes + "block", "Yes"),
                new XElement(itunes + "type", "episodic"),
                new XElement(itunes + "image", new XAttribute("href", artwork)),
                new XElement(itunes + "category", new XAttribute("text", "News")),
                new XElement("image", new XElement("url", artwork), new XElement("title", title), new XElement("link", _Settings.Rest.AppBaseUrl)));
            foreach (MediaAsset m in await FeedEpisodesAsync(ft, 200, token).ConfigureAwait(false))
            {
                Briefing? b = await _Database.Briefings.ReadAsync(m.TenantId, m.BriefingId, token).ConfigureAwait(false);
                string ext = m.ContentType == "audio/wav" ? "wav" : "mp3";
                string enclosure = baseUrl + "/v1.0/public/feeds/" + Uri.EscapeDataString(secret) + "/episodes/" + m.Id + "." + ext;
                string summary = Summary(b?.Markdown);
                XElement item = new XElement("item",
                    new XElement("title", m.Title),
                    new XElement("description", summary),
                    new XElement(itunes + "summary", summary),
                    new XElement("guid", new XAttribute("isPermaLink", "false"), m.Id),
                    new XElement("pubDate", (m.CompletedUtc ?? m.CreatedUtc).ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                    new XElement("enclosure", new XAttribute("url", enclosure), new XAttribute("length", m.Bytes.ToString(CultureInfo.InvariantCulture)), new XAttribute("type", m.ContentType ?? "audio/mpeg")),
                    new XElement(itunes + "duration", ((int)Math.Round(m.DurationSeconds)).ToString(CultureInfo.InvariantCulture)),
                    new XElement(itunes + "explicit", "false"),
                    new XElement(itunes + "episodeType", "full"));
                if (m.ChaptersArtifactId != null)
                    item.Add(new XElement(podcast + "chapters", new XAttribute("url", baseUrl + "/v1.0/public/feeds/" + Uri.EscapeDataString(secret) + "/chapters/" + m.Id + ".json"), new XAttribute("type", "application/json+chapters")));
                channel.Add(item);
            }

            XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "itunes", itunes.NamespaceName), new XAttribute(XNamespace.Xmlns + "podcast", podcast.NamespaceName), channel));
            return doc.Declaration + "\n" + doc.ToString();
        }

        /// <summary>
        /// The feed URL for a secret.
        /// </summary>
        /// <param name="secret">Secret.</param>
        /// <returns>URL.</returns>
        public string FeedUrl(string secret)
        {
            return _Settings.Rest.PublicBaseUrl + "/v1.0/public/feeds/" + Uri.EscapeDataString(secret) + "/podcast.xml";
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

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                if (_OwnsTts && _Tts is IDisposable d) d.Dispose();
                _Http.Dispose();
                QueueSignal.Dispose();
            }

            _Disposed = true;
        }

        private async Task<bool> ProduceAsync(MediaAsset m, CancellationToken token)
        {
            // Returns false when the episode went back to the queue for another attempt.
            try
            {
                Briefing b = await _Database.Briefings.ReadAsync(m.TenantId, m.BriefingId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("The briefing no longer exists.");
                VoiceProfile voice = await ResolveVoiceAsync(m.TenantId, m.VoiceProfileId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("No voice is configured. Add one on the Voices page.");
                if (voice.Provider == TtsProviderEnum.Disabled) throw new InvalidOperationException("The voice '" + voice.Name + "' is disabled.");
                BriefingTemplateDefinition template = await TemplateAsync(b, token).ConfigureAwait(false);

                string script = await ScriptAsync(m, b, template, token).ConfigureAwait(false);
                m.ScriptMarkdown = script;
                m.Status = MediaStatusEnum.Synthesizing;
                m.VoiceProfileId = voice.Id;
                m = await _Database.MediaAssets.UpdateAsync(m, token).ConfigureAwait(false);

                List<ScriptPart> parts = SpeechScript.Parse(script);
                if (parts.Count == 0) throw new InvalidOperationException("The script has nothing to speak.");
                List<SpeechSegment> segments = SpeechScript.Segment(parts, _Settings.Tts.MaxSegmentCharacters);
                byte[][] audio = new byte[segments.Count][];
                using (SemaphoreSlim gate = new SemaphoreSlim(_Settings.Tts.MaxParallelSegments))
                {
                    await Task.WhenAll(segments.Select(async (seg, i) =>
                    {
                        await gate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            string v = seg.Speaker == "HOST_B" ? (voice.SecondaryVoice ?? voice.Voice) : voice.Voice;
                            audio[i] = await SynthesizeWithRetryAsync(voice, v, seg.Text, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    })).ConfigureAwait(false);
                }

                MixResult mix = await Mixer.MixAsync(audio, token).ConfigureAwait(false);
                List<ChapterEntry> chapters = Chapters(parts, segments, mix.SegmentDurations);
                List<BriefingSection> sections = (await _Database.BriefingSections.EnumerateAsync(new BriefingSectionFilter { TenantId = b.TenantId, BriefingId = b.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items;
                string fileBase = (b.ComposedUtc ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-" + TemplateParser.Slug(b.Title);
                Artifact audioFile = await _Artifacts.SaveAsync(new ArtifactWrite
                {
                    TenantId = m.TenantId, UserId = m.UserId, Kind = ArtifactKindEnum.Audio, TopicId = b.TopicId, BriefingId = b.Id, ResearchRequestId = b.ResearchRequestId,
                    FileName = fileBase + "." + mix.Extension, ContentType = mix.ContentType, Extension = mix.Extension, Content = mix.Audio, Pinned = b.Pinned
                }, token).ConfigureAwait(false);
                string chaptersJson = JsonSerializer.Serialize(new { version = "1.2.0", chapters = chapters.Select(c => new { startTime = c.StartTime, endTime = c.EndTime, title = c.Title }) });
                Artifact chaptersFile = await _Artifacts.SaveAsync(new ArtifactWrite
                {
                    TenantId = m.TenantId, UserId = m.UserId, Kind = ArtifactKindEnum.Chapters, TopicId = b.TopicId, BriefingId = b.Id, ResearchRequestId = b.ResearchRequestId,
                    FileName = fileBase + ".chapters.json", ContentType = "application/json+chapters", Extension = "json", Content = Encoding.UTF8.GetBytes(chaptersJson), Pinned = b.Pinned
                }, token).ConfigureAwait(false);
                foreach (MediaChapter old in await ChaptersAsync(m, token).ConfigureAwait(false)) await _Database.MediaChapters.DeleteAsync(old.TenantId, old.Id, token).ConfigureAwait(false);
                for (int i = 0; i < chapters.Count; i++)
                {
                    BriefingSection? sec = sections.FirstOrDefault(s => String.Equals(SpeechScript.Clean(s.Heading), chapters[i].Title, StringComparison.OrdinalIgnoreCase));
                    await _Database.MediaChapters.CreateAsync(new MediaChapter
                    {
                        TenantId = m.TenantId, MediaAssetId = m.Id, BriefingSectionId = sec?.Id, Title = Truncate(chapters[i].Title, 256),
                        StartSeconds = chapters[i].StartTime, EndSeconds = chapters[i].EndTime, SortOrder = i
                    }, token).ConfigureAwait(false);
                }

                m.ArtifactId = audioFile.Id;
                m.ChaptersArtifactId = chaptersFile.Id;
                m.ContentType = mix.ContentType;
                m.Bytes = mix.Audio.LongLength;
                m.DurationSeconds = Math.Round(mix.DurationSeconds, 2);
                m.SegmentCount = segments.Count;
                m.Status = MediaStatusEnum.Ready;
                m.ErrorMessage = null;
                m.LockedUntilUtc = null;
                m.CompletedUtc = DateTime.UtcNow;
                await _Database.MediaAssets.UpdateAsync(m, token).ConfigureAwait(false);
                b.MediaAssetId = m.Id;
                await _Database.Briefings.UpdateAsync(b, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.MediaEpisodes, "outcome", "ready", "format", mix.Extension);
                await NotifyReadyAsync(m, b, token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested))
            {
                bool retry = m.Attempts < _Settings.Tts.MaxAttempts && (ex is TtsException || ex is InvalidOperationException && ex.Message.StartsWith("ffmpeg", StringComparison.Ordinal));
                m.Status = retry ? MediaStatusEnum.Queued : MediaStatusEnum.Failed;
                m.ErrorMessage = ex.Message;
                m.LockedUntilUtc = null;
                await _Database.MediaAssets.UpdateAsync(m, CancellationToken.None).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.MediaEpisodes, "outcome", retry ? "retry" : "failed", "format", "none");
                _Logging.Warn(_Header + "audio " + m.Id + (retry ? " will retry: " : " failed: ") + ex.Message);
                return !retry;
            }
        }

        private async Task NotifyReadyAsync(MediaAsset m, Briefing b, CancellationToken token)
        {
            if (Notifications == null || b.IsDryRun) return;
            try
            {
                Edition? edition = b.EditionId == null ? null : await _Database.Editions.ReadAsync(b.TenantId, b.EditionId, token).ConfigureAwait(false);
                await Notifications.NotifyAsync(m.TenantId, m.UserId, NotificationKindEnum.AudioReady, new NotificationContext
                {
                    Briefing = b,
                    Media = m,
                    Edition = edition,
                    EmailAllowed = edition == null || edition.EmailEnabled,
                    PushAllowed = edition == null || edition.PushEnabled
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // The audio is ready and saved; a notice that could not be sent must not mark it failed.
                _Logging.Warn(_Header + "audio " + m.Id + " is ready but its notice failed: " + ex.Message);
            }
        }

        private async Task<string> ScriptAsync(MediaAsset m, Briefing b, BriefingTemplateDefinition template, CancellationToken token)
        {
            string intro = template.Audio.Intro ?? String.Empty;
            try
            {
                ResolvedPrompt prompt = await _Prompts.ResolveAsync(m.TenantId, "podcast.script", b.TopicId, token).ConfigureAwait(false);
                RenderedPrompt rendered = PromptService.Render(prompt.Definition, new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    { "briefing.markdown", b.Markdown },
                    { "audio.format", m.Format },
                    { "audio.targetMinutes", template.Audio.TargetMinutes },
                    { "audio.intro", intro },
                    { "edition.name", b.Title },
                    { "date.long", (b.ComposedUtc ?? DateTime.UtcNow).ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture) }
                });
                ModelCallResult r = await _Models.ChatAsync(new ModelCallRequest
                {
                    TenantId = m.TenantId, UserId = m.UserId, TopicId = b.TopicId, RunId = b.RunId, BriefingId = b.Id, Task = prompt.Definition.Role, PromptKey = "podcast.script",
                    PromptTemplateVersionId = prompt.Version.Id, SystemPrompt = rendered.System, UserPrompt = rendered.User, IsDryRun = b.IsDryRun
                }, token).ConfigureAwait(false);
                if (SpeechScript.Parse(r.Text).Sum(p => p.Lines.Count) >= 2)
                {
                    m.ScriptFromModel = true;
                    return r.Text.Trim();
                }
            }
            catch (Exception ex) when (ex is ModelCallException || ex is StructuredOutputException)
            {
                _Logging.Debug(_Header + "script for " + m.Id + " written without a model: " + ex.Message);
            }

            m.ScriptFromModel = false;
            return SpeechScript.FromBriefing(b.Markdown, m.Format, intro);
        }

        private async Task<byte[]> SynthesizeWithRetryAsync(VoiceProfile voice, string v, string text, CancellationToken token)
        {
            TtsRequest req = await RequestAsync(voice, v, text, token).ConfigureAwait(false);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    TtsResult r = await _Tts.SynthesizeAsync(req, token).ConfigureAwait(false);
                    if (WavAudio.Parse(r.Audio) == null) throw new TtsException("The speech endpoint did not return WAV audio (" + r.ContentType + ").");
                    return r.Audio;
                }
                catch (TtsException ex) when (attempt < 3 && (ex.StatusCode == 0 || ex.StatusCode == 429 || ex.StatusCode >= 500))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * attempt), token).ConfigureAwait(false);
                }
            }
        }

        private async Task<TtsRequest> RequestAsync(VoiceProfile voice, string v, string text, CancellationToken token)
        {
            return new TtsRequest
            {
                Endpoint = String.IsNullOrWhiteSpace(voice.Endpoint) ? _Settings.Tts.Endpoint : voice.Endpoint,
                ApiKey = await _Vault.ResolveAsync(voice.TenantId, voice.CredentialId, token).ConfigureAwait(false),
                Model = voice.Model,
                Voice = v,
                Speed = voice.Speed,
                Format = "wav",
                Text = text,
                TimeoutSeconds = _Settings.Tts.TimeoutSeconds
            };
        }

        private async Task<List<VoiceProfile>> ProfilesAsync(string tenantId, CancellationToken token)
        {
            List<VoiceProfile> all = (await _Database.VoiceProfiles.EnumerateAsync(new VoiceProfileFilter { TenantId = tenantId, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            if (all.Count == 0 && await EnsureDefaultVoiceAsync(tenantId, token).ConfigureAwait(false))
                all = (await _Database.VoiceProfiles.EnumerateAsync(new VoiceProfileFilter { TenantId = tenantId, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            return all;
        }

        private static bool IsServerProfile(VoiceProfile p)
        {
            return p.Provider == TtsProviderEnum.OpenAiCompatibleSpeech && String.IsNullOrWhiteSpace(p.Endpoint);
        }

        private List<string> ConfiguredVoices()
        {
            return new[] { _Settings.Tts.Voice, _Settings.Tts.SecondaryVoice }
                .Where(v => !String.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.Ordinal).ToList();
        }

        private async Task RequireOfferedAsync(string voice, CancellationToken token)
        {
            List<string> offered = await ListedVoicesAsync(token).ConfigureAwait(false) ?? ConfiguredVoices();
            if (!offered.Contains(voice, StringComparer.Ordinal))
                throw new ArgumentException("The speech server does not offer a voice named " + (voice.Length > 80 ? voice.Substring(0, 80) : voice) + ". See GET /v1.0/api/voices/available.");
        }

        private async Task<List<string>?> ListedVoicesAsync(CancellationToken token)
        {
            string endpoint = _Settings.Tts.Endpoint;
            lock (_VoiceListLock)
            {
                // A failed listing is asked again sooner than a good one.
                double keep = _VoiceList != null ? _VoiceListCacheSeconds : Math.Min(_VoiceListCacheSeconds, 30);
                if (String.Equals(_VoiceListEndpoint, endpoint, StringComparison.Ordinal) && (DateTime.UtcNow - _VoiceListUtc).TotalSeconds < keep)
                    return _VoiceList == null ? null : new List<string>(_VoiceList);
            }

            List<string>? voices = null;
            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, endpoint.TrimEnd('/') + "/audio/voices"))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(_VoiceListTimeoutSeconds));
                    using (HttpResponseMessage resp = await _Http.SendAsync(msg, timeout.Token).ConfigureAwait(false))
                    {
                        if (resp.IsSuccessStatusCode) voices = ParseVoices(await resp.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                voices = null;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is UriFormatException)
            {
                // Unreachable, or not an address a request can be sent to: there is no listing.
                voices = null;
            }

            if (voices != null && voices.Count == 0) voices = null;
            lock (_VoiceListLock)
            {
                _VoiceListEndpoint = endpoint;
                _VoiceList = voices;
                _VoiceListUtc = DateTime.UtcNow;
            }

            return voices == null ? null : new List<string>(voices);
        }

        private static List<string>? ParseVoices(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement list = doc.RootElement;
                    if (list.ValueKind == JsonValueKind.Object)
                    {
                        if (list.TryGetProperty("voices", out JsonElement voices)) list = voices;
                        else if (list.TryGetProperty("data", out JsonElement data)) list = data;
                    }

                    if (list.ValueKind != JsonValueKind.Array) return null;
                    List<string> ids = new List<string>();
                    foreach (JsonElement item in list.EnumerateArray())
                    {
                        string? id = null;
                        if (item.ValueKind == JsonValueKind.String) id = item.GetString();
                        else if (item.ValueKind == JsonValueKind.Object)
                        {
                            foreach (string key in new[] { "id", "voice_id", "name" })
                            {
                                if (item.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
                                {
                                    id = value.GetString();
                                    break;
                                }
                            }
                        }

                        id = id?.Trim();
                        if (!String.IsNullOrEmpty(id) && id.Length <= 100 && !ids.Contains(id, StringComparer.Ordinal)) ids.Add(id);
                        if (ids.Count >= 500) break;
                    }

                    return ids;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<VoiceProfile?> ResolveVoiceAsync(string tenantId, string? voiceProfileId, CancellationToken token)
        {
            if (!String.IsNullOrEmpty(voiceProfileId))
            {
                VoiceProfile? chosen = await _Database.VoiceProfiles.ReadAsync(tenantId, voiceProfileId, token).ConfigureAwait(false);
                if (chosen != null) return chosen;
            }

            List<VoiceProfile> all = (await _Database.VoiceProfiles.EnumerateAsync(new VoiceProfileFilter { TenantId = tenantId, PageSize = 100 }, token).ConfigureAwait(false)).Items;
            if (all.Count == 0 && await EnsureDefaultVoiceAsync(tenantId, token).ConfigureAwait(false))
                all = (await _Database.VoiceProfiles.EnumerateAsync(new VoiceProfileFilter { TenantId = tenantId, PageSize = 100 }, token).ConfigureAwait(false)).Items;
            return all.FirstOrDefault(x => x.IsDefault) ?? all.OrderBy(x => x.CreatedUtc).FirstOrDefault();
        }

        private async Task<BriefingTemplateDefinition> TemplateAsync(Briefing b, CancellationToken token)
        {
            if (!String.IsNullOrEmpty(b.BriefingTemplateVersionId))
            {
                BriefingTemplateVersion? v = await _Database.BriefingTemplateVersions.ReadAsync(b.TenantId, b.BriefingTemplateVersionId, token).ConfigureAwait(false);
                if (v != null) return TemplateParser.Parse(v.Content);
            }

            return TemplateParser.Parse(DefaultTemplateCatalog.Get(b.Kind == BriefingKindEnum.Edition ? DefaultTemplateCatalog.MorningNews : DefaultTemplateCatalog.RunBriefing));
        }

        private static List<ChapterEntry> Chapters(List<ScriptPart> parts, List<SpeechSegment> segments, List<double> durations)
        {
            List<ChapterEntry> chapters = new List<ChapterEntry>();
            double t = 0;
            for (int p = 0; p < parts.Count; p++)
            {
                double start = t;
                for (int i = 0; i < segments.Count; i++) if (segments[i].PartIndex == p) t += durations[i];
                if (t > start) chapters.Add(new ChapterEntry { StartTime = Math.Round(start, 2), EndTime = Math.Round(t, 2), Title = parts[p].Heading });
            }

            return chapters;
        }

        private static string Summary(string? markdown)
        {
            string text = SafeMarkdown.ToText(markdown);
            string[] lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            string body = lines.Length > 1 ? lines[1] : (lines.FirstOrDefault() ?? String.Empty);
            return body.Length > 600 ? body.Substring(0, 600) : body;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        #endregion
    }
}
