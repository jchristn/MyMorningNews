namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Loads settings from the JSON file, applies environment overrides, describes fields for the dashboard, and saves
    /// validated updates. Environment-overridden values always win and are never written to the file. Secrets are
    /// masked when read and only replaced when a new value is supplied.
    /// Thread safety: reads are lock-free; updates are serialized.
    /// </summary>
    public class SettingsService
    {
        #region Public-Members

        /// <summary>
        /// Mask returned in place of secret values.
        /// </summary>
        public const string SecretMask = "********";

        /// <summary>
        /// Settings in effect (file values plus environment overrides). Live updates mutate this instance.
        /// </summary>
        public ServerSettings Current { get; }

        /// <summary>
        /// Settings file path.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Paths overridden by environment variables.
        /// </summary>
        public IReadOnlyCollection<string> OverriddenPaths
        {
            get { return _Overridden.ToList(); }
        }

        #endregion

        #region Private-Members

        private readonly ServerSettings _FileSettings;
        private readonly HashSet<string> _Overridden = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _PendingRestart = new HashSet<string>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private readonly Func<string, string?> _GetEnvironment;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Load settings from a file, creating it with defaults when missing.
        /// </summary>
        /// <param name="filePath">Settings file path.</param>
        /// <param name="getEnvironment">Environment lookup; defaults to <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
        /// <exception cref="ArgumentNullException">filePath is null or empty.</exception>
        /// <exception cref="JsonException">The file is not valid settings JSON.</exception>
        public SettingsService(string filePath, Func<string, string?>? getEnvironment = null)
        {
            if (String.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            FilePath = filePath;
            _GetEnvironment = getEnvironment ?? Environment.GetEnvironmentVariable;

            if (File.Exists(filePath))
            {
                _FileSettings = Serializer.Deserialize<ServerSettings>(File.ReadAllText(filePath)) ?? new ServerSettings();
            }
            else
            {
                _FileSettings = new ServerSettings();
                string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(filePath, Serializer.Serialize(_FileSettings, true));
            }

            Current = Clone(_FileSettings);
            ApplyEnvironment(Current);
        }

        /// <summary>
        /// Create from an in-memory settings object (tests and embedded use). Nothing is read from or written to disk until an update.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="filePath">File path used when saving updates.</param>
        /// <returns>Service.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static SettingsService FromSettings(ServerSettings settings, string filePath)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            File.WriteAllText(filePath, Serializer.Serialize(settings, true));
            return new SettingsService(filePath, name => null);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Describe the current settings with secrets masked.
        /// </summary>
        /// <returns>Settings and metadata.</returns>
        public SettingsResponse Describe()
        {
            ServerSettings masked = Clone(Current);
            List<SettingFieldDescriptor> fields = new List<SettingFieldDescriptor>();
            foreach (SettingField field in Fields())
            {
                object? value = field.Property.GetValue(field.Group.GetValue(Current));
                SettingFieldDescriptor d = new SettingFieldDescriptor
                {
                    Path = field.Path,
                    Group = field.GroupName,
                    Name = field.Name,
                    Type = TypeName(field.Property.PropertyType),
                    Description = field.Attribute.Description,
                    RestartRequired = field.Attribute.RestartRequired,
                    Secret = field.Attribute.Secret,
                    OverriddenByEnvironment = _Overridden.Contains(field.Path),
                    EnvironmentVariable = String.IsNullOrEmpty(field.Attribute.EnvironmentVariable) ? null : Constants.EnvironmentPrefix + field.Attribute.EnvironmentVariable,
                    Minimum = Double.IsNaN(field.Attribute.Minimum) ? null : field.Attribute.Minimum,
                    Maximum = Double.IsNaN(field.Attribute.Maximum) ? null : field.Attribute.Maximum,
                    EnumValues = field.Property.PropertyType.IsEnum ? Enum.GetNames(field.Property.PropertyType).ToList() : null
                };

                if (field.Attribute.Secret)
                {
                    d.IsSet = value is string s && s.Length > 0;
                    if (d.IsSet) field.Property.SetValue(field.Group.GetValue(masked), SecretMask);
                }

                fields.Add(d);
            }

            return new SettingsResponse { Settings = masked, Fields = fields, PendingRestart = _PendingRestart.OrderBy(p => p).ToList() };
        }

        /// <summary>
        /// Save an update. Values overridden by the environment are ignored. Secret values equal to the mask or empty are
        /// left unchanged. Live fields apply immediately; restart-required fields are saved and reported as pending.
        /// </summary>
        /// <param name="incoming">Full settings object from the caller.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentNullException">incoming is null.</exception>
        public async Task<SettingsUpdateResult> UpdateAsync(ServerSettings incoming, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(incoming);
            SettingsUpdateResult result = new SettingsUpdateResult();
            await _Lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ServerSettings candidate = Clone(_FileSettings);
                List<SettingField> changedLive = new List<SettingField>();

                foreach (SettingField field in Fields())
                {
                    if (_Overridden.Contains(field.Path)) continue;
                    object? newValue = field.Property.GetValue(field.Group.GetValue(incoming));
                    if (field.Attribute.Secret && (newValue == null || (newValue is string s && (s.Length == 0 || s == SecretMask)))) continue;

                    object? fileValue = field.Property.GetValue(field.Group.GetValue(candidate));
                    if (SameValue(fileValue, newValue)) continue;

                    try
                    {
                        field.Property.SetValue(field.Group.GetValue(candidate), newValue);
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
                    {
                        result.Errors[field.Path] = ErrorText.Plain(ex.InnerException);
                        continue;
                    }

                    result.Changed.Add(field.Path);
                    if (field.Attribute.RestartRequired) result.PendingRestart.Add(field.Path);
                    else changedLive.Add(field);
                }

                if (result.Errors.Count > 0)
                {
                    result.Success = false;
                    result.Changed.Clear();
                    result.PendingRestart.Clear();
                    return result;
                }

                if (result.Changed.Count == 0) return result;

                string json = Serializer.Serialize(candidate, true);
                await FileReplacer.WriteAllTextAsync(FilePath, json, token).ConfigureAwait(false);

                foreach (SettingField field in Fields())
                {
                    object? value = field.Property.GetValue(field.Group.GetValue(candidate));
                    field.Property.SetValue(field.Group.GetValue(_FileSettings), value);
                }

                foreach (SettingField field in changedLive)
                {
                    object? value = field.Property.GetValue(field.Group.GetValue(candidate));
                    field.Property.SetValue(field.Group.GetValue(Current), value);
                    result.AppliedLive.Add(field.Path);
                }

                foreach (string path in result.PendingRestart) _PendingRestart.Add(path);
                return result;
            }
            finally
            {
                _Lock.Release();
            }
        }

        #endregion

        #region Private-Methods

        private static ServerSettings Clone(ServerSettings settings)
        {
            return Serializer.Deserialize<ServerSettings>(Serializer.Serialize(settings)) ?? new ServerSettings();
        }

        private void ApplyEnvironment(ServerSettings settings)
        {
            foreach (SettingField field in Fields())
            {
                if (String.IsNullOrEmpty(field.Attribute.EnvironmentVariable)) continue;
                string? raw = _GetEnvironment(Constants.EnvironmentPrefix + field.Attribute.EnvironmentVariable);
                if (String.IsNullOrEmpty(raw)) continue;
                object? converted = Convert(raw, field.Property.PropertyType);
                if (converted == null) continue;
                field.Property.SetValue(field.Group.GetValue(settings), converted);
                _Overridden.Add(field.Path);
            }
        }

        private static object? Convert(string raw, Type type)
        {
            if (type == typeof(string)) return raw;
            if (type == typeof(int)) return Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
            if (type == typeof(bool)) return Boolean.TryParse(raw, out bool b) ? b : null;
            if (type == typeof(double)) return Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
            if (type.IsEnum) return Enum.TryParse(type, raw, true, out object? e) ? e : null;
            return null;
        }

        private static bool SameValue(object? a, object? b)
        {
            if (a is IEnumerable<string> la && b is IEnumerable<string> lb) return la.SequenceEqual(lb, StringComparer.Ordinal);
            return Object.Equals(a, b);
        }

        private static string TypeName(Type type)
        {
            if (typeof(IEnumerable<string>).IsAssignableFrom(type) && type != typeof(string)) return "list";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(double) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type.IsEnum) return "enum";
            return "string";
        }

        private static List<SettingField> Fields()
        {
            List<SettingField> fields = new List<SettingField>();
            foreach (PropertyInfo group in typeof(ServerSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!group.PropertyType.IsClass || group.PropertyType == typeof(string)) continue;
                foreach (PropertyInfo prop in group.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    SettingAttribute? attr = prop.GetCustomAttribute<SettingAttribute>();
                    if (attr == null || !prop.CanWrite) continue;
                    fields.Add(new SettingField(group, prop, attr));
                }
            }

            return fields;
        }

        #endregion
    }
}
