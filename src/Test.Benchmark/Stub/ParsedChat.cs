namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// The parts of an OpenAI-compatible chat request the stub model needs.
    /// </summary>
    public class ParsedChat
    {
        #region Public-Members

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Whether the client asked for a stream.
        /// </summary>
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Whether tools were offered (structured output through a tool call).
        /// </summary>
        public bool Tools { get; set; } = false;

        /// <summary>
        /// Whether the system prompt asks for JSON in text (it contains "JSON Schema").
        /// </summary>
        public bool JsonMode { get; set; } = false;

        /// <summary>
        /// The first system message.
        /// </summary>
        public string System { get; set; } = String.Empty;

        /// <summary>
        /// All user messages, joined by blank lines.
        /// </summary>
        public string User { get; set; } = String.Empty;

        /// <summary>
        /// Tool result messages in the conversation.
        /// </summary>
        public int ToolResults { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Parse a request body.
        /// </summary>
        /// <param name="root">Request JSON root.</param>
        /// <returns>Parsed request.</returns>
        public static ParsedChat Parse(JsonElement root)
        {
            ParsedChat chat = new ParsedChat();
            chat.Model = root.TryGetProperty("model", out JsonElement m) && m.ValueKind == JsonValueKind.String ? m.GetString() ?? String.Empty : String.Empty;
            chat.Stream = root.TryGetProperty("stream", out JsonElement s) && s.ValueKind == JsonValueKind.True;
            chat.Tools = root.TryGetProperty("tools", out JsonElement t) && t.ValueKind == JsonValueKind.Array && t.GetArrayLength() > 0;
            List<string> users = new List<string>();
            if (root.TryGetProperty("messages", out JsonElement messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement msg in messages.EnumerateArray())
                {
                    string role = msg.TryGetProperty("role", out JsonElement r) ? r.GetString() ?? String.Empty : String.Empty;
                    string content = Content(msg);
                    if (role == "system" && chat.System.Length == 0) chat.System = content;
                    else if (role == "user") users.Add(content);
                    else if (role == "tool") chat.ToolResults++;
                }
            }

            chat.User = String.Join("\n\n", users);
            chat.JsonMode = chat.System.Contains("JSON Schema", StringComparison.Ordinal);
            return chat;
        }

        #endregion

        #region Private-Methods

        private static string Content(JsonElement msg)
        {
            if (!msg.TryGetProperty("content", out JsonElement c)) return String.Empty;
            if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? String.Empty;
            if (c.ValueKind != JsonValueKind.Array) return String.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (JsonElement part in c.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String) sb.Append(part.GetString());
                else if (part.TryGetProperty("text", out JsonElement text) && text.ValueKind == JsonValueKind.String) sb.Append(text.GetString());
            }

            return sb.ToString();
        }

        #endregion
    }
}
