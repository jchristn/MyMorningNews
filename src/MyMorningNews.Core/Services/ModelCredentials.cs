namespace MyMorningNews.Core.Services
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Rules for model endpoint secrets: which sign-in methods a provider accepts, how a secret is stored in the vault, and
    /// a check that a pasted secret looks like what the method expects. Thread safety: stateless.
    /// </summary>
    public static class ModelCredentials
    {
        /// <summary>
        /// Vault kind for a method's secret.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <returns>Kind.</returns>
        public static VaultCredentialKindEnum KindFor(ModelAuthMethodEnum method)
        {
            switch (method)
            {
                case ModelAuthMethodEnum.AccessToken: return VaultCredentialKindEnum.BearerToken;
                case ModelAuthMethodEnum.AuthFile: return VaultCredentialKindEnum.SignInFile;
                default: return VaultCredentialKindEnum.ApiKey;
            }
        }

        /// <summary>
        /// Vault provider label for a model provider.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>Vault provider.</returns>
        public static VaultProviderEnum VaultProviderFor(ModelProviderEnum provider)
        {
            switch (provider)
            {
                case ModelProviderEnum.Anthropic:
                case ModelProviderEnum.ClaudeCode: return VaultProviderEnum.Anthropic;
                case ModelProviderEnum.OpenAi:
                case ModelProviderEnum.Codex: return VaultProviderEnum.OpenAi;
                case ModelProviderEnum.Gemini:
                case ModelProviderEnum.GeminiCli: return VaultProviderEnum.Gemini;
                case ModelProviderEnum.XAi: return VaultProviderEnum.XAi;
                case ModelProviderEnum.OllamaCloud: return VaultProviderEnum.OllamaCloud;
                case ModelProviderEnum.OpenAiCompatible: return VaultProviderEnum.OpenAiCompatible;
                default: return VaultProviderEnum.Custom;
            }
        }

        /// <summary>
        /// Whether a provider accepts a method.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="method">Method.</param>
        /// <returns>True when accepted.</returns>
        public static bool Accepts(ModelProviderEnum provider, ModelAuthMethodEnum method)
        {
            return AgentCliService.MethodsFor(provider).Contains(method);
        }

        /// <summary>
        /// Check a pasted secret. Sign-in files must be JSON objects with the fields the CLI writes; keys and tokens must be
        /// one line.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="method">Method.</param>
        /// <param name="secret">Secret.</param>
        /// <returns>A message describing the problem, or null when it looks right.</returns>
        public static string? Check(ModelProviderEnum provider, ModelAuthMethodEnum method, string? secret)
        {
            if (!AgentCliService.NeedsSecret(method)) return null;
            string value = (secret ?? String.Empty).Trim();
            if (value.Length == 0) return "Paste " + AgentCliService.Describe(method, provider) + ".";
            if (method != ModelAuthMethodEnum.AuthFile)
            {
                if (value.Contains('\n') || value.Contains(' ')) return "The " + (method == ModelAuthMethodEnum.AccessToken ? "token" : "API key") + " should be one line with no spaces. Copy it again.";
                if (provider == ModelProviderEnum.ClaudeCode && method == ModelAuthMethodEnum.AccessToken && value.StartsWith("sk-ant-api", StringComparison.Ordinal))
                    return "That looks like an Anthropic API key, not a claude setup-token token. Choose \"Anthropic API key\" instead.";
                return null;
            }

            string file = AgentCliService.AuthFileName(provider) ?? "sign-in file";
            JsonElement root;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(value))
                {
                    root = doc.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                return "That is not the contents of " + file + ". Open the file and paste everything in it, starting with {.";
            }

            if (root.ValueKind != JsonValueKind.Object) return "That is not the contents of " + file + ". It should be a JSON object.";
            switch (provider)
            {
                case ModelProviderEnum.Codex:
                    if (!root.TryGetProperty("tokens", out JsonElement _) && !root.TryGetProperty("OPENAI_API_KEY", out JsonElement _))
                        return "This auth.json has no sign-in in it. Run codex login on your computer first, then paste ~/.codex/auth.json.";
                    break;
                case ModelProviderEnum.GeminiCli:
                    if (!root.TryGetProperty("refresh_token", out JsonElement _) && !root.TryGetProperty("access_token", out JsonElement _))
                        return "This oauth_creds.json has no Google sign-in in it. Sign in with gemini on your computer first, then paste ~/.gemini/oauth_creds.json.";
                    break;
            }

            return null;
        }
    }
}
