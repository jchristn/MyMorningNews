namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Readable labels for speech server voice IDs. Kokoro names its voices with a two letter prefix (language, then
    /// f for female or m for male) and a name, such as af_heart. Thread safety: safe for concurrent use.
    /// </summary>
    public static class VoiceNames
    {
        private static readonly Dictionary<char, string> _Languages = new Dictionary<char, string>
        {
            { 'a', "American English" },
            { 'b', "British English" },
            { 'e', "Spanish" },
            { 'f', "French" },
            { 'h', "Hindi" },
            { 'i', "Italian" },
            { 'j', "Japanese" },
            { 'p', "Brazilian Portuguese" },
            { 'z', "Mandarin Chinese" }
        };

        /// <summary>
        /// A readable label for a voice ID: "Heart (American English, female)" for af_heart. An ID that does not follow
        /// Kokoro's naming is returned unchanged.
        /// </summary>
        /// <param name="voice">Voice ID. Null or empty returns an empty string.</param>
        /// <returns>Label. Never null.</returns>
        public static string Describe(string? voice)
        {
            string id = (voice ?? String.Empty).Trim();
            if (id.Length < 4 || id[2] != '_') return id;
            char gender = Char.ToLowerInvariant(id[1]);
            if (!_Languages.TryGetValue(Char.ToLowerInvariant(id[0]), out string? language) || (gender != 'f' && gender != 'm')) return id;
            string name = id.Substring(3).Replace('_', ' ').Trim();
            if (name.Length == 0) return id;
            foreach (char c in name)
            {
                if (!Char.IsLetterOrDigit(c) && c != ' ') return id;
            }

            name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
            return name + " (" + language + ", " + (gender == 'f' ? "female" : "male") + ")";
        }
    }
}
