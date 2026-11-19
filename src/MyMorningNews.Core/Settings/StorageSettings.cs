namespace MyMorningNews.Core.Settings
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Artifact storage and retention: briefing Markdown and rendered HTML, podcast audio, and exports. Every value can be
    /// set in the server JSON file or with a MYMORNINGNEWS_STORAGE_* environment variable.
    /// </summary>
    public class StorageSettings
    {
        #region Public-Members

        /// <summary>
        /// Storage provider. Default Local.
        /// </summary>
        [Setting(Description = "Artifact storage provider: Local or S3.", RestartRequired = true, EnvironmentVariable = "STORAGE_PROVIDER")]
        public StorageProviderEnum Provider { get; set; } = StorageProviderEnum.Local;

        /// <summary>
        /// Local directory for artifacts when Provider is Local. Default "./media".
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Local artifact directory.", RestartRequired = true, EnvironmentVariable = "STORAGE_LOCAL_DIRECTORY")]
        public string LocalDirectory
        {
            get { return _LocalDirectory; }
            set { _LocalDirectory = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(LocalDirectory)) : value.Trim(); }
        }

        /// <summary>
        /// S3 bucket name. Required when Provider is S3.
        /// </summary>
        [Setting(Description = "S3 bucket.", RestartRequired = true, EnvironmentVariable = "STORAGE_S3_BUCKET")]
        public string S3Bucket { get; set; } = String.Empty;

        /// <summary>
        /// S3 region, for example us-west-2. Default us-west-2.
        /// </summary>
        [Setting(Description = "S3 region.", RestartRequired = true, EnvironmentVariable = "STORAGE_S3_REGION")]
        public string S3Region { get; set; } = "us-west-2";

        /// <summary>
        /// Custom S3 endpoint for S3-compatible services (MinIO, R2). Empty uses AWS.
        /// </summary>
        [Setting(Description = "S3-compatible endpoint URL (empty for AWS).", RestartRequired = true, EnvironmentVariable = "STORAGE_S3_ENDPOINT")]
        public string S3Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// S3 access key ID. Empty uses the default AWS credential chain (instance role, environment, profile).
        /// </summary>
        [Setting(Description = "S3 access key ID (empty for the default AWS credential chain).", Secret = true, RestartRequired = true, EnvironmentVariable = "STORAGE_S3_ACCESS_KEY")]
        public string S3AccessKey { get; set; } = String.Empty;

        /// <summary>
        /// S3 secret access key. Empty uses the default AWS credential chain.
        /// </summary>
        [Setting(Description = "S3 secret access key.", Secret = true, RestartRequired = true, EnvironmentVariable = "STORAGE_S3_SECRET_KEY")]
        public string S3SecretKey { get; set; } = String.Empty;

        /// <summary>
        /// Key prefix inside the bucket. Default "mymorningnews/".
        /// </summary>
        [Setting(Description = "Key prefix inside the S3 bucket.", RestartRequired = true, EnvironmentVariable = "STORAGE_S3_PREFIX")]
        public string S3Prefix { get; set; } = "mymorningnews/";

        /// <summary>
        /// Use path-style addressing (needed by MinIO and some S3-compatible services). Default false.
        /// </summary>
        [Setting(Description = "Use S3 path-style addressing.", RestartRequired = true, EnvironmentVariable = "STORAGE_S3_FORCE_PATH_STYLE")]
        public bool S3ForcePathStyle { get; set; } = false;

        /// <summary>
        /// Lifetime of presigned download URLs in minutes. Default 60. Minimum 1, maximum 10080.
        /// </summary>
        [Setting(Description = "Presigned URL lifetime in minutes.", Minimum = 1, Maximum = 10080, EnvironmentVariable = "STORAGE_PRESIGN_MINUTES")]
        public int PresignedUrlMinutes
        {
            get { return _PresignedUrlMinutes; }
            set { _PresignedUrlMinutes = Math.Clamp(value, 1, 10080); }
        }

        /// <summary>
        /// Days generated artifacts (briefing Markdown, rendered HTML, audio, exports) are kept before the retention worker
        /// deletes them. Default 365. Minimum 1, maximum 365. Pinned briefings are kept regardless.
        /// </summary>
        [Setting(Description = "Days to keep reports and audio.", Minimum = 1, Maximum = 365, EnvironmentVariable = "STORAGE_ARTIFACT_RETENTION_DAYS")]
        public int ArtifactRetentionDays
        {
            get { return _ArtifactRetentionDays; }
            set { _ArtifactRetentionDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Most briefings a tenant can pin past the retention window. Default 50, minimum 0, maximum 10000.
        /// The hosted service overrides this per plan with the artifacts.pinned.max entitlement.
        /// </summary>
        public int MaxPinnedArtifacts
        {
            get { return _MaxPinnedArtifacts; }
            set { _MaxPinnedArtifacts = Math.Clamp(value, 0, 10000); }
        }

        #endregion

        #region Private-Members

        private string _LocalDirectory = "./media";
        private int _MaxPinnedArtifacts = 50;
        private int _PresignedUrlMinutes = 60;
        private int _ArtifactRetentionDays = 365;

        #endregion
    }
}
