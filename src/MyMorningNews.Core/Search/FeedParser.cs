namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// Parses RSS 2.0, RSS 1.0 (RDF), and Atom documents (including arXiv responses) into search hits.
    /// Thread safety: thread safe.
    /// </summary>
    public static class FeedParser
    {
        private static readonly Regex _Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex _Spaces = new Regex("\\s+", RegexOptions.Compiled);

        /// <summary>
        /// Parse a feed document.
        /// </summary>
        /// <param name="xml">Document text.</param>
        /// <param name="provider">Provider label for the hits.</param>
        /// <returns>Hits in document order; empty when the document is not a feed.</returns>
        public static List<SearchHit> Parse(string? xml, string provider)
        {
            List<SearchHit> hits = new List<SearchHit>();
            if (String.IsNullOrWhiteSpace(xml)) return hits;
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml, LoadOptions.None);
            }
            catch (XmlException)
            {
                return hits;
            }

            IEnumerable<XElement> items = doc.Descendants().Where(e => e.Name.LocalName == "item" || e.Name.LocalName == "entry");
            foreach (XElement item in items)
            {
                string title = Text(Child(item, "title"));
                string link = Link(item);
                if (link.Length == 0) continue;
                string summary = Text(Child(item, "summary") ?? Child(item, "description") ?? Child(item, "content"));
                DateTime? published = Date(Child(item, "published") ?? Child(item, "pubDate") ?? Child(item, "updated") ?? Child(item, "date"));
                hits.Add(new SearchHit
                {
                    Url = link,
                    Title = title.Length == 0 ? link : title,
                    Snippet = summary.Length > 600 ? summary.Substring(0, 600) + "..." : (summary.Length == 0 ? null : summary),
                    PublishedUtc = published,
                    Rank = hits.Count + 1,
                    Provider = provider
                });
            }

            return hits;
        }

        private static XElement? Child(XElement e, string localName)
        {
            return e.Elements().FirstOrDefault(c => c.Name.LocalName == localName);
        }

        private static string Text(XElement? e)
        {
            if (e == null) return String.Empty;
            string raw = System.Net.WebUtility.HtmlDecode(_Tags.Replace(e.Value, " "));
            return _Spaces.Replace(raw, " ").Trim();
        }

        private static string Link(XElement item)
        {
            foreach (XElement l in item.Elements().Where(c => c.Name.LocalName == "link"))
            {
                string? rel = l.Attribute("rel")?.Value;
                string? href = l.Attribute("href")?.Value;
                if (!String.IsNullOrWhiteSpace(href) && (rel == null || rel == "alternate")) return href.Trim();
                if (String.IsNullOrWhiteSpace(href) && !String.IsNullOrWhiteSpace(l.Value)) return l.Value.Trim();
            }

            string? first = item.Elements().FirstOrDefault(c => c.Name.LocalName == "link")?.Attribute("href")?.Value;
            if (!String.IsNullOrWhiteSpace(first)) return first.Trim();
            string guid = Text(Child(item, "guid") ?? Child(item, "id"));
            return guid.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? guid : String.Empty;
        }

        private static DateTime? Date(XElement? e)
        {
            if (e == null || String.IsNullOrWhiteSpace(e.Value)) return null;
            string v = e.Value.Trim();
            if (DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dto)) return dto.UtcDateTime;
            string trimmed = Regex.Replace(v, "\\s+[A-Z]{2,4}$", " +0000");
            if (DateTimeOffset.TryParseExact(trimmed, new[] { "ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, d MMM yyyy HH:mm:ss zzz", "dd MMM yyyy HH:mm:ss zzz" }, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dto)) return dto.UtcDateTime;
            return null;
        }
    }
}
