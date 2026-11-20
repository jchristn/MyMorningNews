namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Reference;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// The deterministic stand-in for a language model. It recognizes each default prompt by its wording and answers
    /// from the prompt text alone: it never reads the probe labels. Judgments are lexical: relevance is the cosine of
    /// feature-hashed word vectors between the text and the topic (name, brief, and dimensions); the dedupe judge says
    /// Update when the new finding carries a number the old one lacks, Duplicate when the word sets overlap by half or
    /// more, and Different otherwise. The one scripted behavior is the quote mode per page (BenchmarkItem.Quotes),
    /// which simulates a model that misquotes, so evidence verification can be measured; every quote returned is logged
    /// in the ledger with whether it should verify. Thread safety: safe for concurrent requests.
    /// </summary>
    public class StubModelBrain
    {
        #region Public-Members

        /// <summary>
        /// Relevance below which extraction returns no findings ("if a document has nothing relevant, return no
        /// findings"). Default 0.08, minimum 0, maximum 1.
        /// </summary>
        public double ExtractFloor
        {
            get { return _ExtractFloor; }
            set { _ExtractFloor = Math.Clamp(value, 0, 1); }
        }

        /// <summary>
        /// Relevance that maps to a score of 100; the score is linear below it. Default 0.45, minimum 0.05, maximum 1.
        /// </summary>
        public double ScoreSaturation
        {
            get { return _ScoreSaturation; }
            set { _ScoreSaturation = Math.Clamp(value, 0.05, 1); }
        }

        /// <summary>
        /// The scope guardrail the product is expected to prepend to every system prompt, or empty to skip the check.
        /// </summary>
        public string ExpectedGuardrail { get; set; } = String.Empty;

        /// <summary>
        /// Sentence used by the fabricated quote modes. It appears in no page.
        /// </summary>
        public const string FabricatedQuote = "Officials also confirmed a forty percent increase in the program budget for the coming year.";

        #endregion

        #region Private-Members

        private static readonly Regex _Dimension = new Regex(@"^- (.+?) \((Exclude|Low|Normal|High)\)(?:: ([^\n]*))?", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _FindingLine = new Regex(@"^- \[[A-Za-z]+, [0-9]+\] (.+?)(?:: |$)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _Today = new Regex(@"Today is ([0-9]{4}-[0-9]{2}-[0-9]{2})", RegexOptions.Compiled);
        private static readonly Regex _Number = new Regex(@"[0-9][0-9,]*", RegexOptions.Compiled);
        private readonly DatasetIndex? _Index;
        private readonly StubLedger _Ledger;
        private double _ExtractFloor = 0.08;
        private double _ScoreSaturation = 0.45;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="index">Dataset index for quote modes, or null to quote verbatim everywhere.</param>
        /// <param name="ledger">Ledger.</param>
        /// <exception cref="ArgumentNullException">ledger is null.</exception>
        public StubModelBrain(DatasetIndex? index, StubLedger ledger)
        {
            _Index = index;
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Answer a chat request.
        /// </summary>
        /// <param name="chat">Parsed request.</param>
        /// <returns>Answer.</returns>
        /// <exception cref="ArgumentNullException">chat is null.</exception>
        public StubAnswer Answer(ParsedChat chat)
        {
            ArgumentNullException.ThrowIfNull(chat);
            string u = chat.User;
            string all = chat.System + "\n" + u;
            if (all.Contains("You are the search step of a research agent", StringComparison.Ordinal)) return Structured("agentic-search", new { picks = new List<object>() });
            if (u.Contains("Plan at most", StringComparison.Ordinal)) return Queries(u);
            if (u.Contains("Return up to 5 findings", StringComparison.Ordinal)) return Extract(u);
            if (u.Contains("Give a relevance score from 0 to 100", StringComparison.Ordinal)) return Score(u);
            if (u.Contains("Answer Duplicate, Update, or Different", StringComparison.Ordinal)) return Judge(u);
            if (u.Contains("Write a summary of 3 to 6 sentences", StringComparison.Ordinal)) return Synthesize(u);
            if (u.Contains("For each prediction you can judge", StringComparison.Ordinal)) return Structured("review-predictions", new { reviews = new List<object>() });
            if (u.Contains("Make 0 to 5 new predictions", StringComparison.Ordinal)) return Predict(u);
            if (u.Contains("Give up to 5 suggestions", StringComparison.Ordinal)) return Suggest(u);
            if (all.Contains("You label the reasons readers give", StringComparison.Ordinal)) return Structured("tag-reasons", new { items = new List<object>() });
            if (all.Contains("into short preference statements", StringComparison.Ordinal)) return Structured("distill-preferences", new { preferences = new List<object>() });
            if (all.Contains("You write one section of a personal briefing", StringComparison.Ordinal)) return Prose("briefing-section", Section(u));
            if (all.Contains("You write the opening of a personal briefing", StringComparison.Ordinal)) return Prose("briefing-opening", "The most important development today is summarized below, with the evidence behind it.");
            if (all.Contains("You turn a written briefing into a script", StringComparison.Ordinal)) return Prose("podcast-script", Script(u));
            if (all.Contains("You write the lead paragraph of an email", StringComparison.Ordinal)) return Prose("email-lead", "New findings arrived for this topic. The strongest are listed below with their sources.");
            if (chat.Tools || chat.JsonMode) return Structured("unknown", new { answer = "ready" });
            return Prose("unknown", "ready");
        }

        /// <summary>
        /// Relevance of a text to a topic description: cosine of feature-hashed word vectors.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="topic">Topic name, brief, and dimension names.</param>
        /// <returns>Relevance, 0 to 1.</returns>
        public static double Relevance(string text, string topic)
        {
            return Math.Max(0, TextTools.Similarity(text, topic));
        }

        /// <summary>
        /// The score the stub gives a relevance value.
        /// </summary>
        /// <param name="relevance">Relevance.</param>
        /// <returns>Score, 0 to 100.</returns>
        public int ScoreFor(double relevance)
        {
            return (int)Math.Round(Math.Clamp(100.0 * relevance / ScoreSaturation, 0, 100), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The query plan the stub returns for a topic: the topic name, then one query per non-excluded dimension made of
        /// the dimension name and its description (or the topic name when it has none).
        /// </summary>
        /// <param name="topicName">Topic name.</param>
        /// <param name="dimensions">Dimension names and descriptions (description may be empty).</param>
        /// <param name="max">Maximum queries.</param>
        /// <returns>Queries; entry i + 1 belongs to dimension i.</returns>
        public static List<string> PlanQueries(string topicName, IReadOnlyList<KeyValuePair<string, string>> dimensions, int max)
        {
            ArgumentNullException.ThrowIfNull(dimensions);
            List<string> q = new List<string> { topicName };
            foreach (KeyValuePair<string, string> d in dimensions) q.Add(d.Key + " " + (String.IsNullOrWhiteSpace(d.Value) ? topicName : d.Value.Trim().TrimEnd('.')));
            return q.Take(Math.Max(1, max)).ToList();
        }

        #endregion

        #region Private-Methods

        private StubAnswer Queries(string u)
        {
            string topic = Line(u, "Topic: ");
            int max = Int32.TryParse(Regex.Match(u, @"Plan at most ([0-9]+)").Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 8;
            List<KeyValuePair<string, string>> dims = Dimensions(Between(u, "Dimensions and weights:\n", "\n\n"));
            List<object> queries = new List<object>();
            List<string> planned = PlanQueries(topic, dims, max);
            for (int i = 0; i < planned.Count; i++)
                queries.Add(new { query = planned[i], dimension = i == 0 ? null : dims[i - 1].Key, rationale = i == 0 ? "The topic itself." : "Covers this dimension." });
            return Structured("queries", new { queries });
        }

        private StubAnswer Extract(string u)
        {
            string topic = Line(u, "Topic: ");
            string brief = Between(u, "Brief:\n", "\n\nDimensions:");
            List<string> dims = DimensionNames(Between(u, "Dimensions:\n", "\n\nConstraints:"));
            string title = Line(u, "Document: ");
            string url = Line(u, "URL: ");
            string text = Between(u, "\nText:\n", "\n\nReturn up to 5 findings");
            string topicText = topic + " " + brief + " " + String.Join(" ", dims);
            if (Relevance(title + " " + text, topicText) < ExtractFloor) return Structured("extract", new { findings = new List<object>() });

            List<string> paragraphs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => !String.Equals(p, title, StringComparison.Ordinal)).ToList();
            List<string> firsts = paragraphs.Select(p => TextTools.Sentences(p).FirstOrDefault() ?? p).Where(s => s.Split(' ').Length >= 3).ToList();
            string summary = String.Join(" ", firsts.Take(2));
            string? dimension = dims.Select(d => new { d, s = TextTools.Similarity(title + " " + summary, d) }).Where(x => x.s > 0).OrderByDescending(x => x.s).ThenBy(x => x.d, StringComparer.Ordinal).Select(x => x.d).FirstOrDefault();

            string corpus = String.Empty;
            string item = String.Empty;
            string mode = "verbatim";
            if (_Index != null && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && _Index.TryPage(uri.AbsolutePath, out corpus, out item))
                mode = _Index.Item(corpus, item)?.Quotes ?? "verbatim";
            List<KeyValuePair<string, bool>> quotes = QuotesFor(mode, firsts);
            foreach (KeyValuePair<string, bool> q in quotes)
                _Ledger.Quotes.Enqueue(new StubQuoteRecord { Tag = _Ledger.CurrentTag, Corpus = corpus, Item = item, Topic = topic, Quote = q.Key, Genuine = q.Value, Mode = mode });

            object finding = new
            {
                title = title.Length == 0 ? "Untitled development" : title,
                summary = summary.Length == 0 ? title : summary,
                whyItMatters = dimension == null ? "It bears on the topic brief." : "It bears on " + dimension + ".",
                dimension,
                category = "News",
                quotes = quotes.Select(q => q.Key).ToList()
            };
            return Structured("extract", new { findings = new List<object> { finding } });
        }

        private static List<KeyValuePair<string, bool>> QuotesFor(string mode, List<string> firsts)
        {
            List<KeyValuePair<string, bool>> q = new List<KeyValuePair<string, bool>>();
            string a = firsts.Count > 0 ? firsts[0] : String.Empty;
            string b = firsts.Count > 1 ? firsts[1] : String.Empty;
            switch (mode)
            {
                case "typography":
                    if (a.Length > 0) q.Add(new KeyValuePair<string, bool>(Typography(a), true));
                    if (b.Length > 0) q.Add(new KeyValuePair<string, bool>(Typography(b), true));
                    break;
                case "fabricated":
                    if (a.Length > 0) q.Add(new KeyValuePair<string, bool>(a, true));
                    q.Add(new KeyValuePair<string, bool>(FabricatedQuote, false));
                    break;
                case "fabricated-only":
                    q.Add(new KeyValuePair<string, bool>(FabricatedQuote, false));
                    break;
                case "altered":
                    if (a.Length > 0) q.Add(new KeyValuePair<string, bool>(Alter(a), false));
                    if (b.Length > 0) q.Add(new KeyValuePair<string, bool>(b, true));
                    break;
                case "short":
                    if (a.Length > 0) q.Add(new KeyValuePair<string, bool>(a.Split(' ')[0].Trim('.', ',', ';', ':'), false));
                    if (b.Length > 0) q.Add(new KeyValuePair<string, bool>(b, true));
                    break;
                default:
                    if (a.Length > 0) q.Add(new KeyValuePair<string, bool>(a, true));
                    if (b.Length > 0) q.Add(new KeyValuePair<string, bool>(b, true));
                    break;
            }

            return q;
        }

        private static string Typography(string s)
        {
            StringBuilder sb = new StringBuilder();
            bool open = true;
            foreach (char c in s)
            {
                if (c == '"') { sb.Append(open ? '\u201C' : '\u201D'); open = !open; }
                else if (c == '\'') sb.Append('\u2019');
                else if (c == ' ') sb.Append("  ");
                else sb.Append(c);
            }

            string t = sb.ToString();
            return t.Length > 0 ? Char.ToLowerInvariant(t[0]) + t.Substring(1) : t;
        }

        private static string Alter(string s)
        {
            Match m = _Number.Match(s);
            if (m.Success)
            {
                char[] digits = m.Value.ToCharArray();
                for (int i = 0; i < digits.Length; i++) if (Char.IsDigit(digits[i])) { digits[i] = (char)('0' + ((digits[i] - '0' + 1) % 10)); break; }
                return s.Substring(0, m.Index) + new string(digits) + s.Substring(m.Index + m.Length);
            }

            int space = s.IndexOf(' ');
            return space < 0 ? "Not " + s : s.Substring(0, space) + " never" + s.Substring(space);
        }

        private StubAnswer Score(string u)
        {
            string topic = Line(u, "Topic: ");
            string brief = Between(u, "Brief:\n", "\n\nDimensions and weights:");
            List<string> dims = DimensionNames(Between(u, "Dimensions and weights:\n", "\n\nConstraints:"));
            string title = Line(u, "Title: ");
            string summary = Line(u, "Summary: ");
            double rel = Relevance(title + " " + summary, topic + " " + brief + " " + String.Join(" ", dims));
            int score = ScoreFor(rel);
            return Structured("score", new { score, confidence = 0.8, rationale = "Lexical match with the brief: " + rel.ToString("0.000", CultureInfo.InvariantCulture) + "." });
        }

        private static StubAnswer Judge(string u)
        {
            string candidate = Between(u, "New finding:\n", "\n\nEarlier finding");
            int start = u.IndexOf("Earlier finding", StringComparison.Ordinal);
            string rest = start < 0 ? String.Empty : u.Substring(start);
            string existing = Between(rest, "):\n", "\n\nAnswer Duplicate");
            List<string> fresh = TextTools.NewNumbers(candidate, existing);
            double overlap = TextTools.Jaccard(candidate, existing);
            string verdict = fresh.Count > 0 ? "Update" : overlap >= 0.5 ? "Duplicate" : "Different";
            string reason = verdict == "Update" ? "It adds " + String.Join(", ", fresh) + "." : verdict == "Duplicate" ? "Same facts (word overlap " + overlap.ToString("0.00", CultureInfo.InvariantCulture) + ")." : "A different development.";
            return Structured("dedupe-judge", new { verdict, reason });
        }

        private static StubAnswer Synthesize(string u)
        {
            List<string> titles = FindingTitles(u);
            string summary = titles.Count == 0 ? "Nothing new stood out in this run." : titles.Count + " findings stood out. The lead development: " + titles[0] + ".";
            List<object> themes = titles.Count == 0 ? new List<object>() : new List<object> { new { name = "Highlights", description = "The strongest findings of the run.", findings = titles.Take(3).ToList() } };
            return Structured("synthesize", new { summary, themes });
        }

        private static StubAnswer Predict(string u)
        {
            List<string> titles = FindingTitles(u);
            DateTime today = Today(u);
            List<object> predictions = titles.Count == 0 ? new List<object>() : new List<object>
            {
                new { statement = "Coverage of \"" + titles[0] + "\" will continue through " + today.AddDays(60).ToString("MMMM yyyy", CultureInfo.InvariantCulture) + ".", rationale = "It is the lead development.", confidence = 0.6, horizon = today.AddDays(60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), findings = new List<string> { titles[0] } }
            };
            return Structured("predict", new { predictions });
        }

        private static StubAnswer Suggest(string u)
        {
            List<string> titles = FindingTitles(u);
            List<object> suggestions = titles.Count == 0 ? new List<object>() : new List<object>
            {
                new { title = "Read the source behind the lead finding", description = "Open the source for \"" + titles[0] + "\" and note the dates.", rationale = "It is the lead development.", priority = "Medium", effort = "Minutes", findings = new List<string> { titles[0] } }
            };
            return Structured("suggest", new { suggestions });
        }

        private static string Section(string u)
        {
            return "The items below matter for this topic [1].";
        }

        private static string Script(string u)
        {
            bool twoHost = Line(u, "Format: ").Contains("two", StringComparison.OrdinalIgnoreCase);
            if (twoHost) return "## Introduction\nHOST_A: Good morning. Here is what moved.\nHOST_B: What changed first?\n## The news\nHOST_A: The lead story is in the briefing, according to its sources.\nHOST_B: Good to know.";
            return "## Introduction\nGood morning. Here is what moved.\n\n## The news\nThe lead story is in the briefing, according to its sources.";
        }

        private static StubAnswer Structured(string prompt, object value)
        {
            return new StubAnswer { Prompt = prompt, Arguments = Json.Serialize(value) };
        }

        private static StubAnswer Prose(string prompt, string text)
        {
            return new StubAnswer { Prompt = prompt, Text = text };
        }

        private static List<string> FindingTitles(string u)
        {
            return _FindingLine.Matches(u).Select(m => m.Groups[1].Value.Trim()).Where(t => t.Length > 0).ToList();
        }

        private static List<KeyValuePair<string, string>> Dimensions(string block)
        {
            return _Dimension.Matches(block).Where(m => m.Groups[2].Value != "Exclude").Select(m => new KeyValuePair<string, string>(m.Groups[1].Value.Trim(), m.Groups[3].Success ? m.Groups[3].Value.Trim() : String.Empty)).ToList();
        }

        private static List<string> DimensionNames(string block)
        {
            return _Dimension.Matches(block).Where(m => m.Groups[2].Value != "Exclude").Select(m => m.Groups[1].Value.Trim()).ToList();
        }

        private static DateTime Today(string u)
        {
            Match m = _Today.Match(u);
            return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime d) ? d : DateTime.UtcNow.Date;
        }

        /// <summary>
        /// The rest of the first line starting with a prefix.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="prefix">Line prefix.</param>
        /// <returns>Value, or empty.</returns>
        internal static string Line(string text, string prefix)
        {
            int i = text.StartsWith(prefix, StringComparison.Ordinal) ? 0 : text.IndexOf("\n" + prefix, StringComparison.Ordinal);
            if (i < 0) return String.Empty;
            int start = i == 0 && text.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length : i + 1 + prefix.Length;
            int end = text.IndexOf('\n', start);
            return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
        }

        /// <summary>
        /// Text between two markers (the first occurrence of each).
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="start">Start marker.</param>
        /// <param name="end">End marker.</param>
        /// <returns>Text, or empty.</returns>
        internal static string Between(string text, string start, string end)
        {
            int s = text.IndexOf(start, StringComparison.Ordinal);
            if (s < 0) return String.Empty;
            s += start.Length;
            int e = text.IndexOf(end, s, StringComparison.Ordinal);
            return (e < 0 ? text.Substring(s) : text.Substring(s, e - s)).Trim();
        }

        #endregion
    }
}
