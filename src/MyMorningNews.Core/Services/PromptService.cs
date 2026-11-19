namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Versioned, editable prompts. Tenants receive copies of the shipped defaults; topics may override any key.
    /// Every save creates a new version; rollback and reset also create new versions so history is never lost.
    /// Thread safety: safe for concurrent use; concurrent saves of one template are serialized by the version unique index.
    /// </summary>
    public class PromptService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public PromptService(DatabaseDriverBase database)
        {
            ArgumentNullException.ThrowIfNull(database);
            _Database = database;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create tenant copies of any default prompts the tenant lacks.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of prompts created.</returns>
        public async Task<int> EnsureDefaultsAsync(string tenantId, CancellationToken token = default)
        {
            int created = 0;
            foreach (PromptDefinition def in DefaultPromptCatalog.All().Values)
            {
                if (await FindAsync(tenantId, def.Key, null, token).ConfigureAwait(false) != null) continue;
                await CreateAsync(tenantId, def.Key, null, def.Content, null, token).ConfigureAwait(false);
                created++;
            }

            return created;
        }

        /// <summary>
        /// Resolve a prompt for use: the topic override when present, otherwise the tenant prompt (created from the default when missing).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="key">Prompt key.</param>
        /// <param name="topicId">Topic, when any.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Resolved prompt.</returns>
        /// <exception cref="KeyNotFoundException">The key is unknown.</exception>
        public async Task<ResolvedPrompt> ResolveAsync(string tenantId, string key, string? topicId, CancellationToken token = default)
        {
            PromptTemplate? template = null;
            if (!String.IsNullOrEmpty(topicId)) template = await FindAsync(tenantId, key, topicId, token).ConfigureAwait(false);
            template ??= await FindAsync(tenantId, key, null, token).ConfigureAwait(false);
            if (template == null)
            {
                PromptDefinition? def = DefaultPromptCatalog.Get(key) ?? throw new KeyNotFoundException("Unknown prompt key " + key + ".");
                template = await CreateAsync(tenantId, key, null, def.Content, null, token).ConfigureAwait(false);
            }

            PromptTemplateVersion version = await CurrentVersionAsync(template, token).ConfigureAwait(false);
            return new ResolvedPrompt(template, version, PromptParser.Parse(version.Content));
        }

        /// <summary>
        /// Render a definition.
        /// </summary>
        /// <param name="definition">Definition.</param>
        /// <param name="values">Variable values.</param>
        /// <returns>Rendered prompt.</returns>
        public static RenderedPrompt Render(PromptDefinition definition, IReadOnlyDictionary<string, object?> values)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(values);
            return new RenderedPrompt
            {
                System = PromptRenderer.Render(definition.System, values),
                User = PromptRenderer.Render(definition.User, values),
                UnknownVariables = PromptRenderer.Unknown(definition)
            };
        }

        /// <summary>
        /// Validate prompt content for a key.
        /// </summary>
        /// <param name="key">Expected key.</param>
        /// <param name="content">Content.</param>
        /// <returns>Field errors; empty when valid.</returns>
        public static Dictionary<string, string> Validate(string key, string content)
        {
            Dictionary<string, string> errors = new Dictionary<string, string>();
            PromptDefinition def;
            try
            {
                def = PromptParser.Parse(content);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                errors["content"] = ex.Message;
                return errors;
            }

            if (!String.Equals(def.Key, key, StringComparison.Ordinal)) errors["key"] = "The front matter key must be " + key + ".";
            if (String.IsNullOrWhiteSpace(def.User)) errors["user"] = "The prompt needs a ## User section.";
            List<string> unknown = PromptRenderer.Unknown(def);
            if (unknown.Count > 0) errors["variables"] = "Undeclared variables: " + String.Join(", ", unknown) + ". Add them to the variables list or remove them.";
            return errors;
        }

        /// <summary>
        /// Read the template with its current content.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public async Task<PromptTemplateResponse> DescribeAsync(PromptTemplate template, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            PromptTemplateVersion version = await CurrentVersionAsync(template, token).ConfigureAwait(false);
            PromptDefinition def = PromptParser.Parse(version.Content);
            return new PromptTemplateResponse
            {
                Template = template,
                Content = version.Content,
                Variables = def.Variables,
                DefaultContent = DefaultPromptCatalog.Get(template.Key)?.Content ?? String.Empty
            };
        }

        /// <summary>
        /// Save a new version.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="content">Content (validate first).</param>
        /// <param name="changeNote">Change note.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        public async Task<PromptTemplateVersion> SaveAsync(PromptTemplate template, string content, string? changeNote, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            PromptDefinition def = PromptParser.Parse(content);
            PromptTemplateVersion version = await _Database.PromptTemplateVersions.CreateAsync(new PromptTemplateVersion
            {
                TenantId = template.TenantId,
                PromptTemplateId = template.Id,
                Version = template.CurrentVersion + 1,
                Content = content,
                ChangeNote = changeNote,
                CreatedByUserId = userId
            }, token).ConfigureAwait(false);
            template.CurrentVersion = version.Version;
            template.Name = String.IsNullOrWhiteSpace(def.Name) ? template.Name : def.Name;
            template.Role = def.Role;
            template.OutputType = String.IsNullOrWhiteSpace(def.Output) ? null : def.Output;
            template.IsCustomized = !String.Equals(Normalize(content), Normalize(DefaultPromptCatalog.Get(template.Key)?.Content), StringComparison.Ordinal);
            await _Database.PromptTemplates.UpdateAsync(template, token).ConfigureAwait(false);
            return version;
        }

        /// <summary>
        /// Restore an earlier version's content as a new version.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="version">Version to restore.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        /// <exception cref="KeyNotFoundException">The version does not exist.</exception>
        public async Task<PromptTemplateVersion> RollbackAsync(PromptTemplate template, int version, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            PromptTemplateVersion? old = (await VersionsAsync(template, token).ConfigureAwait(false)).FirstOrDefault(v => v.Version == version)
                ?? throw new KeyNotFoundException("Version " + version + " was not found.");
            return await SaveAsync(template, old.Content, "Rolled back to version " + version + ".", userId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Restore the shipped default as a new version.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        /// <exception cref="KeyNotFoundException">No default exists for the key.</exception>
        public async Task<PromptTemplateVersion> ResetAsync(PromptTemplate template, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            PromptDefinition def = DefaultPromptCatalog.Get(template.Key) ?? throw new KeyNotFoundException("No default exists for " + template.Key + ".");
            return await SaveAsync(template, def.Content, "Reset to default.", userId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a topic override from the tenant prompt (or supplied content).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="key">Key.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="content">Initial content, or null to copy the tenant prompt.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Override template.</returns>
        public async Task<PromptTemplate> CreateOverrideAsync(string tenantId, string key, string topicId, string? content, string? userId, CancellationToken token = default)
        {
            ResolvedPrompt tenant = await ResolveAsync(tenantId, key, null, token).ConfigureAwait(false);
            return await CreateAsync(tenantId, key, topicId, content ?? tenant.Version.Content, userId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// All versions of a template, newest first.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Versions.</returns>
        public async Task<List<PromptTemplateVersion>> VersionsAsync(PromptTemplate template, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            EnumerationResult<PromptTemplateVersion> versions = await _Database.PromptTemplateVersions.EnumerateAsync(
                new PromptTemplateVersionFilter { TenantId = template.TenantId, PromptTemplateId = template.Id, PageSize = 1000, SortBy = "version" }, token).ConfigureAwait(false);
            return versions.Items;
        }

        /// <summary>
        /// Find a template by key and scope.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="key">Key.</param>
        /// <param name="topicId">Topic, or null for the tenant prompt.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Template, or null.</returns>
        public async Task<PromptTemplate?> FindAsync(string tenantId, string key, string? topicId, CancellationToken token = default)
        {
            EnumerationResult<PromptTemplate> found = await _Database.PromptTemplates.EnumerateAsync(
                new PromptTemplateFilter { TenantId = tenantId, Key = key, PageSize = 100 }, token).ConfigureAwait(false);
            return found.Items.FirstOrDefault(t => String.Equals(t.TopicId, topicId, StringComparison.Ordinal));
        }

        #endregion

        #region Private-Methods

        private async Task<PromptTemplate> CreateAsync(string tenantId, string key, string? topicId, string content, string? userId, CancellationToken token)
        {
            PromptDefinition def = PromptParser.Parse(content);
            PromptTemplate template = await _Database.PromptTemplates.CreateAsync(new PromptTemplate
            {
                TenantId = tenantId,
                Key = key,
                TopicId = topicId,
                Name = String.IsNullOrWhiteSpace(def.Name) ? key : def.Name,
                Role = def.Role,
                OutputType = String.IsNullOrWhiteSpace(def.Output) ? null : def.Output,
                CurrentVersion = 1,
                IsCustomized = !String.Equals(Normalize(content), Normalize(DefaultPromptCatalog.Get(key)?.Content), StringComparison.Ordinal)
            }, token).ConfigureAwait(false);
            await _Database.PromptTemplateVersions.CreateAsync(new PromptTemplateVersion
            {
                TenantId = tenantId,
                PromptTemplateId = template.Id,
                Version = 1,
                Content = content,
                ChangeNote = topicId == null ? "Created from the default." : "Topic override created.",
                CreatedByUserId = userId
            }, token).ConfigureAwait(false);
            return template;
        }

        private async Task<PromptTemplateVersion> CurrentVersionAsync(PromptTemplate template, CancellationToken token)
        {
            List<PromptTemplateVersion> versions = await VersionsAsync(template, token).ConfigureAwait(false);
            return versions.FirstOrDefault(v => v.Version == template.CurrentVersion) ?? versions.FirstOrDefault()
                ?? throw new KeyNotFoundException("Prompt " + template.Key + " has no versions.");
        }

        private static string Normalize(string? content)
        {
            return (content ?? String.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        }

        #endregion
    }
}
