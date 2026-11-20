namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Briefing templates: validate, create a private copy of a shared template, read, save a version, versions, rollback,
    /// export, delete, and 404. Shared templates are never changed.
    /// </summary>
    public class TemplatesTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Briefing templates CRUD, versions, validate, export, rollback";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TemplatesTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            EnumerationResult<BriefingTemplate> shared = await StepAsync(context, result, "GET templates (shared)", () => c.Templates.ListAsync(new TemplateListOptions { Scope = "shared" }, token)).ConfigureAwait(false);
            BriefingTemplate? source = shared.Items.FirstOrDefault();
            if (source == null) throw new SkipTestException("The tenant has no shared template to copy.");
            string original = await StepAsync(context, result, "GET template export", () => c.Templates.ExportAsync(source.Id, token)).ConfigureAwait(false);
            Helpers.True(original.StartsWith("---", StringComparison.Ordinal), "export is Markdown with front matter");

            TemplateValidationResponse ok = await StepAsync(context, result, "POST validate", () => c.Templates.ValidateAsync(new TemplateValidateRequest { Content = original }, token)).ConfigureAwait(false);
            Helpers.True(ok.Valid && ok.Sections.Count > 0, "the shipped template is valid");
            TemplateValidationResponse bad = await StepAsync(context, result, "POST validate (broken directive)", () => c.Templates.ValidateAsync(new TemplateValidateRequest
            {
                Content = "---\nname: x\n---\n# T\n## S\n<!-- mmn:section source=nonsense -->\n"
            }, token)).ConfigureAwait(false);
            Helpers.True(!bad.Valid && bad.Problems.Count > 0, "a broken directive is reported");

            string name = context.Name("template");
            TemplateDetailResponse created = await StepAsync(context, result, "POST template", () => c.Templates.CreateAsync(new TemplateCreateRequest { Content = original, Name = name, Shared = false }, token)).ConfigureAwait(false);
            string id = created.Template.Id;
            context.Cleanup.Add("template:" + id, ct => c.Templates.DeleteAsync(id, ct));
            Helpers.True(!created.Shared && created.Template.UserId == context.AdminUserId, "a private template");
            Helpers.Equal(name, created.Template.Name, "the name given on create");

            TemplateDetailResponse read = await StepAsync(context, result, "GET template", () => c.Templates.GetAsync(id, token)).ConfigureAwait(false);
            int v1 = read.Template.CurrentVersion;
            string changed = original.Replace("## Sources", "## Sources and notes", StringComparison.Ordinal);
            TemplateDetailResponse saved = await StepAsync(context, result, "PUT template (new version)", () => c.Templates.UpdateAsync(id, new TemplateSaveRequest { Content = changed, ChangeNote = "harness" }, token)).ConfigureAwait(false);
            Helpers.True(saved.Template.CurrentVersion > v1, "the version number grows");
            List<BriefingTemplateVersion> versions = await StepAsync(context, result, "GET template versions", () => c.Templates.ListVersionsAsync(id, token)).ConfigureAwait(false);
            Helpers.True(versions.Count >= 2, "two versions");
            TemplateDetailResponse rolled = await StepAsync(context, result, "POST rollback", () => c.Templates.RollbackAsync(id, v1, token)).ConfigureAwait(false);
            Helpers.Equal(read.Content, rolled.Content, "rollback restores the first content");

            EnumerationResult<BriefingTemplate> mine = await StepAsync(context, result, "GET templates (mine)", () => c.Templates.ListAsync(new TemplateListOptions { Scope = "mine", PageSize = 1000 }, token)).ConfigureAwait(false);
            Helpers.Contains(mine.Items, t => t.Id == id, "the template");

            await StepAsync(context, result, "DELETE template", () => c.Templates.DeleteAsync(id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("template:" + id);
            await StepAsync(context, result, "GET deleted template (404)", () => Helpers.ExpectErrorAsync(() => c.Templates.GetAsync(id, token), 404)).ConfigureAwait(false);
        }
    }
}
