namespace MyMorningNews.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Npgsql;

    /// <summary>
    /// Describes database errors for people: which field a unique value collided on, and whether the database is
    /// unreachable rather than the request being wrong.
    /// Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public static class DatabaseErrorDescriber
    {
        private static readonly Dictionary<string, string> _Nouns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "briefingtemplates", "briefing template" },
            { "credentials", "access key" },
            { "devices", "device" },
            { "editions", "edition" },
            { "modelendpoints", "model endpoint" },
            { "plans", "plan" },
            { "promocodes", "promotional code" },
            { "topicrecipients", "recipient" },
            { "topics", "topic" },
            { "userroles", "role" },
            { "users", "user" },
            { "vaultcredentials", "vault secret" },
            { "voiceprofiles", "voice" },
            { "searchproviders", "search provider" },
            { "tenants", "tenant" }
        };

        private static readonly Dictionary<string, string> _Fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "name", "name" },
            { "email", "email" },
            { "slug", "name" },
            { "code", "code" },
            { "key", "key" },
            { "expopushtoken", "expoPushToken" },
            { "accesskey", "accessKey" }
        };

        /// <summary>
        /// Field a unique violation collided on, as a request field name (camelCase), or null when it cannot be told.
        /// </summary>
        /// <param name="ex">Unique violation.</param>
        /// <returns>Field name, or null.</returns>
        /// <exception cref="ArgumentNullException">ex is null.</exception>
        public static string? UniqueField(PostgresException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            string? constraint = ex.ConstraintName;
            if (String.IsNullOrEmpty(constraint)) return null;
            string[] parts = constraint.Split('_');
            if (parts.Length < 3) return null;
            string last = parts[parts.Length - 1];
            return _Fields.TryGetValue(last, out string? field) ? field : null;
        }

        /// <summary>
        /// Message for a unique violation, naming the record kind and the field where they can be told.
        /// </summary>
        /// <param name="ex">Unique violation.</param>
        /// <returns>Message, such as "A model endpoint with that name already exists."</returns>
        /// <exception cref="ArgumentNullException">ex is null.</exception>
        public static string UniqueMessage(PostgresException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            string? field = UniqueField(ex);
            string? noun = !String.IsNullOrEmpty(ex.TableName) && _Nouns.TryGetValue(ex.TableName, out string? n) ? n : null;
            if (field != null && noun != null) return Article(noun) + " " + noun + " with that " + field + " already exists. Choose a different " + field + ".";
            if (field != null) return "That " + field + " is already in use. Choose a different " + field + ".";
            return "A record with the same unique value already exists.";
        }

        /// <summary>
        /// Whether an exception means the database cannot be reached or is not accepting work, so the request may succeed
        /// later (503) rather than being wrong.
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>True when the database is unavailable.</returns>
        /// <exception cref="ArgumentNullException">ex is null.</exception>
        public static bool IsUnavailable(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            if (ex is PostgresException pe)
            {
                string state = pe.SqlState ?? String.Empty;
                // 08: connection exceptions; 57P01 to 57P03: shutting down or not yet accepting connections; 53300: too many connections.
                return state.StartsWith("08", StringComparison.Ordinal) || state == "57P01" || state == "57P02" || state == "57P03" || state == "53300";
            }

            return ex is NpgsqlException;
        }

        private static string Article(string noun)
        {
            return noun.Length > 0 && "aeiouAEIOU".IndexOf(noun[0]) >= 0 ? "An" : "A";
        }
    }
}
