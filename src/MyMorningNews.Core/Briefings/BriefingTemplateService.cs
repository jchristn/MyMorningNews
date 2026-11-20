namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// Briefing templates: tenant defaults seeded from the shipped templates (and kept current until customized), user
    /// templates, versions, validation, and resolution for composition. Thread safety: safe for concurrent use.
    /// </summary>
    public class BriefingTemplateService
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public BriefingTemplateService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Create tenant templates for the shipped defaults, and update uncustomized ones to the current default text.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Templates created or updated.</returns>
        public async Task<int> EnsureDefaultsAsync(string tenantId, CancellationToken token = default)
        {
            int changed = 0;
            foreach (KeyValuePair<string, string> d in DefaultTemplateCatalog.All())
            {
                BriefingTemplate? existing = (await _Database.BriefingTemplates.EnumerateAsync(new BriefingTemplateFilter { TenantId = tenantId, DefaultKey = d.Key, PageSize = 1 }, token).ConfigureAwait(false)).Items
                    .FirstOrDefault(t => t.UserId == null);
                if (existing == null)
                {
                    BriefingTemplateDefinition def = TemplateParser.Parse(d.Value);
                    BriefingTemplate t = await _Database.BriefingTemplates.CreateAsync(new BriefingTemplate
                    {
                        TenantId = tenantId,
                        Name = def.Name,
                        Description = def.Description,
                        DefaultKey = d.Key,
                        IsDefault = d.Key == DefaultTemplateCatalog.MorningNews,
                        CurrentVersion = 1
                    }, token).ConfigureAwait(false);
                    await _Database.BriefingTemplateVersions.CreateAsync(new BriefingTemplateVersion { TenantId = tenantId, TemplateId = t.Id, Version = 1, Content = d.Value, ChangeNote = "Default." }, token).ConfigureAwait(false);
                    changed++;
                    continue;
                }

                if (existing.Customized) continue;
                BriefingTemplateVersion? current = await CurrentVersionAsync(existing, token).ConfigureAwait(false);
                if (current != null && String.Equals(current.Content, d.Value, StringComparison.Ordinal)) continue;
                await AddVersionAsync(existing, d.Value, "Updated to the current default.", null, false, token).ConfigureAwait(false);
                changed++;
            }

            return changed;
        }

        /// <summary>
        /// Create a template from Markdown. The name comes from the front matter unless given.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner; null for a tenant template.</param>
        /// <param name="content">Template Markdown.</param>
        /// <param name="name">Name override, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Template.</returns>
        /// <exception cref="TemplateInvalidException">The template has errors.</exception>
        /// <exception cref="ConflictException">The name is taken.</exception>
        public async Task<BriefingTemplate> CreateAsync(string tenantId, string? userId, string content, string? name, CancellationToken token = default)
        {
            BriefingTemplateDefinition def = Validate(content);
            string finalName = String.IsNullOrWhiteSpace(name) ? def.Name : name.Trim();
            if (await NameTakenAsync(tenantId, userId, finalName, null, token).ConfigureAwait(false)) throw new ConflictException("You already have a template named '" + finalName + "'.");
            BriefingTemplate t = await _Database.BriefingTemplates.CreateAsync(new BriefingTemplate
            {
                TenantId = tenantId,
                UserId = userId,
                Name = finalName,
                Description = def.Description,
                CurrentVersion = 1,
                Customized = true
            }, token).ConfigureAwait(false);
            await _Database.BriefingTemplateVersions.CreateAsync(new BriefingTemplateVersion { TenantId = tenantId, TemplateId = t.Id, Version = 1, Content = content, ChangeNote = "Created.", CreatedByUserId = userId }, token).ConfigureAwait(false);
            return t;
        }

        /// <summary>
        /// Save a new version. The description follows the front matter; the name follows it only when the front matter
        /// name changed from the previous version, so a name given at creation survives ordinary edits.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="content">Markdown.</param>
        /// <param name="changeNote">What changed.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        /// <exception cref="TemplateInvalidException">The template has errors.</exception>
        public async Task<BriefingTemplateVersion> SaveAsync(BriefingTemplate template, string content, string? changeNote, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            BriefingTemplateDefinition def = Validate(content);
            string? previousName = null;
            BriefingTemplateVersion? current = await CurrentVersionAsync(template, token).ConfigureAwait(false);
            if (current != null)
            {
                try
                {
                    previousName = Validate(current.Content).Name;
                }
                catch (TemplateInvalidException)
                {
                }
            }

            bool renamedInFrontMatter = !String.Equals(def.Name, previousName, StringComparison.Ordinal);
            if (renamedInFrontMatter && !String.Equals(def.Name, template.Name, StringComparison.Ordinal) && !await NameTakenAsync(template.TenantId, template.UserId, def.Name, template.Id, token).ConfigureAwait(false))
                template.Name = def.Name;
            template.Description = def.Description;
            return await AddVersionAsync(template, content, changeNote, userId, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Restore an earlier version as a new version.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="version">Version to restore.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        /// <exception cref="KeyNotFoundException">No such version.</exception>
        public async Task<BriefingTemplateVersion> RollbackAsync(BriefingTemplate template, int version, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            BriefingTemplateVersion v = (await VersionsAsync(template, token).ConfigureAwait(false)).FirstOrDefault(x => x.Version == version)
                ?? throw new KeyNotFoundException("Version " + version + " does not exist.");
            return await AddVersionAsync(template, v.Content, "Restored version " + version + ".", userId, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reset a seeded template to the shipped default.
        /// </summary>
        /// <param name="template">Template with a default key.</param>
        /// <param name="userId">Author.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>New version.</returns>
        /// <exception cref="ConflictException">The template was not seeded from a default.</exception>
        public async Task<BriefingTemplateVersion> ResetAsync(BriefingTemplate template, string? userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (String.IsNullOrEmpty(template.DefaultKey)) throw new ConflictException("Only templates that came from a default can be reset.");
            BriefingTemplateVersion v = await AddVersionAsync(template, DefaultTemplateCatalog.Get(template.DefaultKey), "Reset to the default.", userId, false, token).ConfigureAwait(false);
            return v;
        }

        /// <summary>
        /// Versions, newest first.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Versions.</returns>
        public async Task<List<BriefingTemplateVersion>> VersionsAsync(BriefingTemplate template, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            return (await _Database.BriefingTemplateVersions.EnumerateAsync(new BriefingTemplateVersionFilter { TenantId = template.TenantId, TemplateId = template.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items
                .OrderByDescending(v => v.Version).ToList();
        }

        /// <summary>
        /// The current version.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Version, or null when missing.</returns>
        public async Task<BriefingTemplateVersion?> CurrentVersionAsync(BriefingTemplate template, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            return (await VersionsAsync(template, token).ConfigureAwait(false)).FirstOrDefault(v => v.Version == template.CurrentVersion);
        }

        /// <summary>
        /// Delete a template and its versions. Editions using it fall back to the default.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ConflictException">The template is a tenant default.</exception>
        public async Task DeleteAsync(BriefingTemplate template, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (template.UserId == null && !String.IsNullOrEmpty(template.DefaultKey)) throw new ConflictException("Default templates cannot be deleted. Reset or edit it instead.");
            foreach (BriefingTemplateVersion v in await VersionsAsync(template, token).ConfigureAwait(false))
                await _Database.BriefingTemplateVersions.DeleteAsync(template.TenantId, v.Id, token).ConfigureAwait(false);
            await _Database.BriefingTemplates.DeleteAsync(template.TenantId, template.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolve the template to compose with: the given one when it exists and parses, otherwise the tenant default
        /// with the given key (seeding defaults when missing).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="templateId">Chosen template, or null.</param>
        /// <param name="defaultKey">Fallback default key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Resolved template.</returns>
        public async Task<ResolvedTemplate> ResolveAsync(string tenantId, string? templateId, string defaultKey, CancellationToken token = default)
        {
            if (!String.IsNullOrEmpty(templateId))
            {
                BriefingTemplate? chosen = await _Database.BriefingTemplates.ReadAsync(tenantId, templateId, token).ConfigureAwait(false);
                ResolvedTemplate? r = chosen == null ? null : await TryResolveAsync(chosen, token).ConfigureAwait(false);
                if (r != null) return r;
            }

            BriefingTemplate? fallback = await FindDefaultAsync(tenantId, defaultKey, token).ConfigureAwait(false);
            if (fallback == null)
            {
                await EnsureDefaultsAsync(tenantId, token).ConfigureAwait(false);
                fallback = await FindDefaultAsync(tenantId, defaultKey, token).ConfigureAwait(false);
            }

            ResolvedTemplate? resolved = fallback == null ? null : await TryResolveAsync(fallback, token).ConfigureAwait(false);
            if (resolved != null) return resolved;
            string content = DefaultTemplateCatalog.Get(defaultKey);
            return new ResolvedTemplate { Template = fallback ?? new BriefingTemplate { TenantId = tenantId, Name = defaultKey }, Version = new BriefingTemplateVersion { Content = content }, Definition = TemplateParser.Parse(content) };
        }

        /// <summary>
        /// Parse and require no errors.
        /// </summary>
        /// <param name="content">Markdown.</param>
        /// <returns>Definition.</returns>
        /// <exception cref="TemplateInvalidException">The template has errors.</exception>
        public static BriefingTemplateDefinition Validate(string? content)
        {
            BriefingTemplateDefinition def = TemplateParser.Parse(content);
            if (TemplateParser.HasErrors(def)) throw new TemplateInvalidException(def.Problems);
            return def;
        }

        private async Task<BriefingTemplate?> FindDefaultAsync(string tenantId, string key, CancellationToken token)
        {
            return (await _Database.BriefingTemplates.EnumerateAsync(new BriefingTemplateFilter { TenantId = tenantId, DefaultKey = key, PageSize = 10 }, token).ConfigureAwait(false)).Items
                .FirstOrDefault(t => t.UserId == null);
        }

        private async Task<ResolvedTemplate?> TryResolveAsync(BriefingTemplate t, CancellationToken token)
        {
            BriefingTemplateVersion? v = await CurrentVersionAsync(t, token).ConfigureAwait(false);
            if (v == null) return null;
            BriefingTemplateDefinition def = TemplateParser.Parse(v.Content);
            return TemplateParser.HasErrors(def) ? null : new ResolvedTemplate { Template = t, Version = v, Definition = def };
        }

        private async Task<BriefingTemplateVersion> AddVersionAsync(BriefingTemplate template, string content, string? note, string? userId, bool customized, CancellationToken token)
        {
            int next = (await VersionsAsync(template, token).ConfigureAwait(false)).Select(v => v.Version).DefaultIfEmpty(0).Max() + 1;
            BriefingTemplateVersion v = await _Database.BriefingTemplateVersions.CreateAsync(new BriefingTemplateVersion
            {
                TenantId = template.TenantId,
                TemplateId = template.Id,
                Version = next,
                Content = content,
                ChangeNote = note,
                CreatedByUserId = userId
            }, token).ConfigureAwait(false);
            template.CurrentVersion = next;
            template.Customized = customized;
            await _Database.BriefingTemplates.UpdateAsync(template, token).ConfigureAwait(false);
            return v;
        }

        private async Task<bool> NameTakenAsync(string tenantId, string? userId, string name, string? exceptId, CancellationToken token)
        {
            List<BriefingTemplate> all = (await _Database.BriefingTemplates.EnumerateAsync(new BriefingTemplateFilter { TenantId = tenantId, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            return all.Any(t => t.UserId == userId && t.Id != exceptId && String.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
