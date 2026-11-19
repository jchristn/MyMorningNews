namespace MyMorningNews.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Application meter and activity source, both named "MyMorningNews". Metric names use the mymorningnews. prefix,
    /// which Prometheus renders as mymorningnews_*. Labels are low cardinality: never tenant, topic, or user identifiers.
    /// Instrumentation is best effort and never throws.
    /// Thread safety: instruments are thread safe.
    /// </summary>
    public static class MyMorningNewsTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Meter and activity source name.
        /// </summary>
        public const string Name = "MyMorningNews";

        /// <summary>
        /// Activity source for application spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(Name, Constants.ProductVersion);

        /// <summary>
        /// Meter for application metrics.
        /// </summary>
        public static readonly Meter Meter = new Meter(Name, Constants.ProductVersion);

        /// <summary>
        /// Authentication outcomes by scheme and result.
        /// </summary>
        public static readonly Counter<long> AuthRequests = Meter.CreateCounter<long>("mymorningnews.auth.requests", "{request}", "Authentication outcomes by scheme and result.");

        /// <summary>
        /// Authorization outcomes by result.
        /// </summary>
        public static readonly Counter<long> AuthzRequests = Meter.CreateCounter<long>("mymorningnews.authz.requests", "{request}", "Authorization outcomes by result.");

        /// <summary>
        /// Authorization denials by reason class and required permission.
        /// </summary>
        public static readonly Counter<long> AuthzDenials = Meter.CreateCounter<long>("mymorningnews.authz.denials", "{request}", "Authorization denials.");

        /// <summary>
        /// Session lifecycle events.
        /// </summary>
        public static readonly Counter<long> SessionEvents = Meter.CreateCounter<long>("mymorningnews.session.events", "{event}", "Session lifecycle events.");

        /// <summary>
        /// RBAC and credential control-plane mutations.
        /// </summary>
        public static readonly Counter<long> RbacMutations = Meter.CreateCounter<long>("mymorningnews.rbac.mutations", "{mutation}", "RBAC and credential mutations.");

        /// <summary>
        /// Request history capture outcomes.
        /// </summary>
        public static readonly Counter<long> RequestCaptures = Meter.CreateCounter<long>("mymorningnews.request_history.captures", "{request}", "Request history capture outcomes.");

        /// <summary>
        /// Background worker iterations by worker and outcome.
        /// </summary>
        public static readonly Counter<long> WorkerIterations = Meter.CreateCounter<long>("mymorningnews.worker.iterations", "{iteration}", "Background worker iterations.");

        /// <summary>
        /// Background worker iteration duration.
        /// </summary>
        public static readonly Histogram<double> WorkerDuration = Meter.CreateHistogram<double>("mymorningnews.worker.duration", "s", "Background worker iteration duration.");

        /// <summary>
        /// Model calls by task, provider, and outcome.
        /// </summary>
        public static readonly Counter<long> LlmCalls = Meter.CreateCounter<long>("mymorningnews.llm.calls", "{call}", "Model calls by task, provider, and outcome.");

        /// <summary>
        /// Model call duration by task and provider.
        /// </summary>
        public static readonly Histogram<double> LlmCallDuration = Meter.CreateHistogram<double>("mymorningnews.llm.call.duration", "s", "Model call duration.");

        /// <summary>
        /// Model tokens by task, provider, and direction (input or output).
        /// </summary>
        public static readonly Counter<long> LlmTokens = Meter.CreateCounter<long>("mymorningnews.llm.tokens", "{token}", "Model tokens.");

        /// <summary>
        /// Structured output repair turns by task and provider.
        /// </summary>
        public static readonly Counter<long> LlmStructuredRepairs = Meter.CreateCounter<long>("mymorningnews.llm.structured_repairs", "{repair}", "Structured output repair turns.");

        /// <summary>
        /// Time to first streamed token by task and provider.
        /// </summary>
        public static readonly Histogram<double> LlmTimeToFirstToken = Meter.CreateHistogram<double>("mymorningnews.llm.time_to_first_token", "s", "Time to first streamed token.");

        /// <summary>
        /// Tool calls returned by models, by task and provider.
        /// </summary>
        public static readonly Counter<long> LlmToolCalls = Meter.CreateCounter<long>("mymorningnews.llm.tool_calls", "{call}", "Tool calls returned by models.");

        /// <summary>
        /// Estimated model cost in USD by task and provider.
        /// </summary>
        public static readonly Counter<double> LlmCostUsd = Meter.CreateCounter<double>("mymorningnews.llm.cost_usd", "{USD}", "Estimated model cost.");

        #endregion

        #region Public-Methods

        /// <summary>
        /// Increment a counter with labels, swallowing instrumentation failures.
        /// </summary>
        /// <param name="counter">Counter.</param>
        /// <param name="labels">Label pairs as name, value, name, value.</param>
        public static void Increment(Counter<long> counter, params string[] labels)
        {
            if (counter == null) return;
            try
            {
                counter.Add(1, ToTags(labels));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a histogram value with labels, swallowing instrumentation failures.
        /// </summary>
        /// <param name="histogram">Histogram.</param>
        /// <param name="value">Value.</param>
        /// <param name="labels">Label pairs as name, value, name, value.</param>
        public static void Record(Histogram<double> histogram, double value, params string[] labels)
        {
            if (histogram == null) return;
            try
            {
                histogram.Record(value, ToTags(labels));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Add a value to a counter with labels, swallowing instrumentation failures.
        /// </summary>
        /// <param name="counter">Counter.</param>
        /// <param name="value">Value.</param>
        /// <param name="labels">Label pairs as name, value, name, value.</param>
        public static void Add(Counter<long> counter, long value, params string[] labels)
        {
            if (counter == null) return;
            try
            {
                counter.Add(value, ToTags(labels));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Add a value to a floating-point counter with labels, swallowing instrumentation failures.
        /// </summary>
        /// <param name="counter">Counter.</param>
        /// <param name="value">Value.</param>
        /// <param name="labels">Label pairs as name, value, name, value.</param>
        public static void AddDouble(Counter<double> counter, double value, params string[] labels)
        {
            if (counter == null) return;
            try
            {
                counter.Add(value, ToTags(labels));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Start an activity. Returns null when nothing is listening.
        /// </summary>
        /// <param name="name">Span name, for example stage:Search or polyprompt chat.</param>
        /// <param name="kind">Activity kind.</param>
        /// <returns>Activity or null.</returns>
        public static Activity? StartSpan(string name, ActivityKind kind = ActivityKind.Internal)
        {
            try
            {
                return ActivitySource.StartActivity(name, kind);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private static TagList ToTags(string[] labels)
        {
            TagList tags = new TagList();
            if (labels == null) return tags;
            for (int i = 0; i + 1 < labels.Length; i += 2) tags.Add(labels[i], labels[i + 1]);
            return tags;
        }

        #endregion
    }
}
