-- My Morning News factory demo data. (c) 2026 Lattice AI LLC
--
-- Run by the factory-seed-data container (psql) after seed.sh has created demo@mymorningnews through the API:
--   psql -v ON_ERROR_STOP=1 --single-transaction -f /seed/demo.sql
--
-- What it adds for the demo user, with no model calls: two topics with dimensions, constraints, and a recipient; both
-- topics linked to the user's Morning News edition; edition and topic schedules (paused, so nothing runs until someone
-- adds a model key and turns them on); three completed runs with stages, queries, results, sources, verified evidence, ranked findings,
-- predictions, a prediction review, and suggestions; one Edition briefing and three Run briefings in Markdown; feedback
-- from several channels; and learned preferences (proposed and accepted). Learning checkpoints are set past the seeded
-- feedback so the learning worker does not call a model for it.
--
-- Every source, product, and organization below is fictional, and every URL uses the reserved .example domain.
-- IDs use the prefixes in src/MyMorningNews.Core/IdPrefixes.cs with a fixed "mmndemo0" body, so the script is
-- idempotent: when the first demo topic exists it stops without changing anything. Times are relative to now().

\set ON_ERROR_STOP on
\set QUIET on

CREATE FUNCTION pg_temp.did(prefix text, n integer) RETURNS text LANGUAGE sql IMMUTABLE AS
$$ SELECT prefix || 'mmndemo0_' || lpad(n::text, 19, '0') $$;

CREATE FUNCTION pg_temp.sha(t text) RETURNS text LANGUAGE sql IMMUTABLE AS
$$ SELECT encode(sha256(convert_to(t, 'UTF8')), 'hex') $$;

SELECT coalesce((SELECT id FROM users WHERE tenantid = 'default' AND email = 'demo@mymorningnews'), '') AS uid \gset
SELECT :'uid' = '' AS no_user \gset
\if :no_user
  \echo '[seed-data] demo@mymorningnews does not exist yet; nothing seeded.'
  \quit
\endif

SELECT EXISTS (SELECT 1 FROM topics WHERE id = pg_temp.did('top_', 1)) AS already \gset
\if :already
  \echo '[seed-data] demo data already present; nothing changed.'
  \quit
\endif

SELECT
  coalesce((SELECT id FROM briefingtemplates WHERE tenantid = 'default' AND defaultkey = 'morning-news' ORDER BY createdutc LIMIT 1), '') AS tpl_morning,
  coalesce((SELECT v.id FROM briefingtemplateversions v JOIN briefingtemplates t ON t.id = v.templateid
            WHERE t.tenantid = 'default' AND t.defaultkey = 'morning-news' ORDER BY v.version DESC LIMIT 1), '') AS tpl_morning_v,
  coalesce((SELECT v.id FROM briefingtemplateversions v JOIN briefingtemplates t ON t.id = v.templateid
            WHERE t.tenantid = 'default' AND t.defaultkey = 'run-briefing' ORDER BY v.version DESC LIMIT 1), '') AS tpl_run_v,
  coalesce((SELECT id FROM searchproviders WHERE tenantid = 'default' AND kind = 'Searxng' ORDER BY createdutc LIMIT 1), '') AS spv_searx,
  to_char(now() AT TIME ZONE 'America/Los_Angeles', 'FMMonth FMDD, YYYY') AS today_long,
  to_char((now() - interval '27 hours') AT TIME ZONE 'America/Los_Angeles', 'FMMonth FMDD, YYYY') AS yesterday_long
\gset

-- Anchor times. Run A1 ran yesterday; runs A2 and B1 ran this morning ahead of the edition.
\set t_setup '(now() - interval ''3 days'')'
\set t_a1 '(now() - interval ''27 hours'')'
\set t_a2 '(now() - interval ''3 hours 10 minutes'')'
\set t_b1 '(now() - interval ''3 hours 6 minutes'')'
\set t_edition '(now() - interval ''2 hours 40 minutes'')'

\echo '[seed-data] seeding demo topics, runs, findings, briefings, and feedback for' :uid

-- Topics --------------------------------------------------------------------------------------------------------

INSERT INTO topics (id, tenantid, userid, name, slug, description, brief, audiencelevel, horizon, language, status, isonetime,
  noveltypreference, emailonrun, emailwhenempty, maxemailfindings, displaythreshold, weightllm, weightfeedback, weightnovelty,
  weightsource, weightrecency, seedqueries, briefingtemplateid, lastrunutc, lastrefinedutc, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('top_', 1), 'default', :'uid', 'AI developer tools', 'ai-developer-tools',
   'Releases, pricing changes, and notable practices in AI coding assistants, editors, and agent frameworks.',
   'I lead a small platform team that runs internal developer tooling for about 40 engineers. Tell me about coding assistant and editor releases we could adopt, agent frameworks we might build on, and pricing or licensing changes that affect a team our size. Skip funding announcements and hype.',
   'Practitioner', 'Weeks', 'en', 'Active', false, 60, true, false, 10, NULL, NULL, NULL, NULL, NULL, NULL,
   ARRAY['AI coding assistant release notes', 'agent framework SDK release', 'coding assistant pricing change'], NULL,
   :t_a2 + interval '3 minutes', :t_setup, true, :t_setup, :t_a2),
  (pg_temp.did('top_', 2), 'default', :'uid', 'Home energy and heat pumps', 'home-energy-and-heat-pumps',
   'Heat pump technology, efficiency rebates, and residential solar and battery news.',
   'Homeowner in the Pacific Northwest planning to replace a gas furnace within a year. I want cold-climate heat pump performance data, rebates I could qualify for in Washington State, and practical solar and battery guidance. Explain jargon briefly.',
   'Beginner', 'Months', 'en', 'Active', false, 50, true, false, 8, NULL, NULL, NULL, NULL, NULL, NULL,
   ARRAY['cold climate heat pump field test', 'Washington heat pump rebate 2027', 'home battery backup sizing'], NULL,
   :t_b1 + interval '3 minutes', :t_setup, true, :t_setup, :t_b1);

INSERT INTO topicdimensions (id, tenantid, topicid, name, description, rationale, weight, includeterms, excludeterms, islocked, origin, sortorder, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('dim_', 1), 'default', pg_temp.did('top_', 1), 'Coding assistants and editors', 'Completion, chat, and agentic editing inside IDEs and editors.',
   'Your team evaluates these tools for daily use.', 'High', ARRAY['coding assistant', 'code completion', 'IDE'], ARRAY[]::text[], false, 'Model', 0, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 2), 'default', pg_temp.did('top_', 1), 'Agent frameworks and SDKs', 'Libraries for tool calling, sandboxes, and multi-step agents.',
   'You may build internal agents on one of these.', 'Normal', ARRAY['agent framework', 'tool calling', 'SDK'], ARRAY[]::text[], false, 'Model', 1, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 3), 'default', pg_temp.did('top_', 1), 'Pricing and licensing', 'Seat, usage, and license changes for developer AI products.',
   'Budget changes for a 40-person org.', 'Normal', ARRAY['pricing', 'seat', 'license'], ARRAY[]::text[], false, 'Model', 2, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 4), 'default', pg_temp.did('top_', 1), 'Funding and acquisitions', 'Venture rounds and acquisitions in the space.',
   'You asked to skip funding news.', 'Exclude', ARRAY['funding', 'acquisition'], ARRAY[]::text[], true, 'User', 3, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 5), 'default', pg_temp.did('top_', 2), 'Cold-climate heat pumps', 'Performance below freezing, sizing, and installation quality.',
   'Your winters drop below 20 F.', 'High', ARRAY['cold climate heat pump', 'COP', 'HSPF'], ARRAY['commercial HVAC'], false, 'Model', 0, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 6), 'default', pg_temp.did('top_', 2), 'Rebates and incentives', 'State, utility, and federal programs for residential electrification.',
   'Rebates change the payback on your furnace replacement.', 'High', ARRAY['rebate', 'incentive', 'tax credit'], ARRAY[]::text[], false, 'Model', 1, true, :t_setup, :t_setup),
  (pg_temp.did('dim_', 7), 'default', pg_temp.did('top_', 2), 'Solar and home batteries', 'Rooftop solar, net metering, and backup batteries.',
   'Useful context for a later solar decision.', 'Normal', ARRAY['rooftop solar', 'net metering', 'home battery'], ARRAY[]::text[], false, 'Model', 2, true, :t_setup, :t_setup);

INSERT INTO topicconstraints (id, tenantid, topicid, dimensionid, kind, value, note, islocked, origin, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('tcn_', 1), 'default', pg_temp.did('top_', 1), NULL, 'ExcludeKeyword', 'funding round', 'No venture news.', true, 'User', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 2), 'default', pg_temp.did('top_', 1), NULL, 'PreferredDomain', 'devtools-weekly.example', 'Reliable release coverage.', false, 'Model', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 3), 'default', pg_temp.did('top_', 1), NULL, 'BlockedDomain', 'hype-aggregator.example', 'Rewrites press releases.', true, 'User', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 4), 'default', pg_temp.did('top_', 1), pg_temp.did('dim_', 1), 'TrackedQuestion', 'Which coding assistants support self-hosted models for regulated teams?', NULL, false, 'Model', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 5), 'default', pg_temp.did('top_', 2), NULL, 'IncludeEntity', 'Washington State', 'Where the house is.', true, 'User', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 6), 'default', pg_temp.did('top_', 2), pg_temp.did('dim_', 5), 'ExcludeKeyword', 'commercial HVAC', NULL, false, 'Model', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 7), 'default', pg_temp.did('top_', 2), pg_temp.did('dim_', 6), 'TrackedQuestion', 'When does the 2027 state heat pump rebate portal open?', NULL, false, 'Model', true, :t_setup, :t_setup),
  (pg_temp.did('tcn_', 8), 'default', pg_temp.did('top_', 2), NULL, 'PreferredDomain', 'energy-desk.example', NULL, false, 'Model', true, :t_setup, :t_setup);

INSERT INTO topicrecipients (id, tenantid, topicid, email, userid, isactive, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('trc_', 1), 'default', pg_temp.did('top_', 1), 'demo@mymorningnews', :'uid', true, :t_setup, :t_setup),
  (pg_temp.did('trc_', 2), 'default', pg_temp.did('top_', 2), 'demo@mymorningnews', :'uid', true, :t_setup, :t_setup);

-- Edition and schedules (paused) -------------------------------------------------------------------------------
-- The server gives every new user a Morning News edition (all topics, daily at 06:30 local). The demo reuses it, links
-- both topics in order, and pauses its schedule so the demo stays static and makes no model calls until someone turns
-- it back on. If it is missing, the demo creates an equivalent one.

SELECT coalesce((SELECT id FROM editions WHERE tenantid = 'default' AND userid = :'uid' AND name = 'Morning News' ORDER BY createdutc LIMIT 1), '') AS edn \gset
SELECT :'edn' = '' AS no_edn \gset
\if :no_edn
  SELECT pg_temp.did('edn_', 1) AS edn \gset
  INSERT INTO editions (id, tenantid, userid, name, description, briefingtemplateid, voiceprofileid, audioenabled, emailenabled, enabled,
    freshresearchbeforecompose, freshresearchleadminutes, lookbackmode, lookbackhours, lastcomposedutc, active, createdutc, lastupdateutc)
  VALUES
    (:'edn', 'default', :'uid', 'Morning News', 'Your daily briefing across all of your active topics.', nullif(:'tpl_morning', ''), NULL,
     true, true, true, true, 60, 'SinceLastEdition', 24, :t_edition, true, :t_setup, :t_edition);
  INSERT INTO schedules (id, tenantid, ownertype, ownerid, userid, name, intervaltype, intervalvalue, minuteoffset, daysofweekmask, daysofmonth,
    timesofday, cronexpression, timezone, startutc, endutc, runonceutc, enabled, missedrunpolicy, nextrunutc, lastrunutc, lockeduntilutc, active, createdutc, lastupdateutc)
  VALUES
    (pg_temp.did('sch_', 1), 'default', 'Edition', :'edn', :'uid', 'Morning News', 'DaysOfWeek', 1, 0, 127, ARRAY[]::integer[],
     ARRAY['06:30'], NULL, 'America/Los_Angeles', NULL, NULL, NULL, false, 'RunOnceOnRecovery', NULL, :t_edition, NULL, true, :t_setup, :t_edition);
\else
  UPDATE editions SET lastcomposedutc = :t_edition, lastupdateutc = :t_edition WHERE id = :'edn';
  UPDATE schedules SET enabled = false, nextrunutc = NULL, lastrunutc = :t_edition, lastupdateutc = now()
  WHERE tenantid = 'default' AND ownertype = 'Edition' AND ownerid = :'edn';
\endif
SELECT coalesce((SELECT id FROM schedules WHERE tenantid = 'default' AND ownertype = 'Edition' AND ownerid = :'edn' ORDER BY createdutc LIMIT 1), '') AS edn_sch \gset

INSERT INTO editiontopics (id, tenantid, editionid, topicid, sortorder, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('edt_', 1), 'default', :'edn', pg_temp.did('top_', 1), 0, true, :t_setup, :t_setup),
  (pg_temp.did('edt_', 2), 'default', :'edn', pg_temp.did('top_', 2), 1, true, :t_setup, :t_setup);

-- A paused twice-weekly research schedule for the home energy topic.
INSERT INTO schedules (id, tenantid, ownertype, ownerid, userid, name, intervaltype, intervalvalue, minuteoffset, daysofweekmask, daysofmonth,
  timesofday, cronexpression, timezone, startutc, endutc, runonceutc, enabled, missedrunpolicy, nextrunutc, lastrunutc, lockeduntilutc, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('sch_', 2), 'default', 'Topic', pg_temp.did('top_', 2), :'uid', 'Monday and Thursday', 'DaysOfWeek', 1, 0, 18, ARRAY[]::integer[],
   ARRAY['05:00'], NULL, 'America/Los_Angeles', NULL, NULL, NULL, false, 'Skip', NULL, NULL, NULL, true, :t_setup, :t_setup);

-- Runs and stages -----------------------------------------------------------------------------------------------

INSERT INTO runs (id, tenantid, topicid, userid, scheduleid, researchrequestid, trigger, status, isdryrun, cancelrequested, queuedutc, startedutc,
  completedutc, lockeduntilutc, workerid, skippedreason, errormessage, summary, durationms, newfindingcount, duplicatecount, sourcecount,
  inputtokens, outputtokens, estimatedcostusd, contextsnapshothash, active, createdutc, lastupdateutc, resumefromstage)
VALUES
  (pg_temp.did('run_', 1), 'default', pg_temp.did('top_', 1), :'uid', NULL, NULL, 'Manual', 'Completed', false, false, :t_a1, :t_a1 + interval '2 seconds',
   :t_a1 + interval '3 minutes 14 seconds', NULL, 'demo-seed', NULL, NULL,
   'Three new findings: Lumen Code 4.2 adds repository-wide edits, Tessel SDK reaches 1.0, and Quill Assistant moves to usage-based seats.',
   192000, 3, 0, 3, 0, 0, NULL, NULL, true, :t_a1, :t_a1 + interval '3 minutes 14 seconds', NULL),
  (pg_temp.did('run_', 2), 'default', pg_temp.did('top_', 1), :'uid', NULL, NULL, 'Edition', 'Completed', false, false, :t_a2, :t_a2 + interval '1 second',
   :t_a2 + interval '2 minutes 51 seconds', NULL, 'demo-seed', NULL, NULL,
   'Three new findings and one duplicate. A survey answers your tracked question on self-hosted models, and Tessel 1.1 ships the sandbox predicted last run.',
   170000, 3, 1, 4, 0, 0, NULL, NULL, true, :t_a2, :t_a2 + interval '2 minutes 51 seconds', NULL),
  (pg_temp.did('run_', 3), 'default', pg_temp.did('top_', 2), :'uid', NULL, NULL, 'Edition', 'Completed', false, false, :t_b1, :t_b1 + interval '1 second',
   :t_b1 + interval '2 minutes 37 seconds', NULL, 'demo-seed', NULL, NULL,
   'Four new findings, led by a two-winter field test of cold-climate heat pumps and the 2027 state rebate portal date.',
   156000, 4, 0, 4, 0, 0, NULL, NULL, true, :t_b1, :t_b1 + interval '2 minutes 37 seconds', NULL);

-- (run, stage, sortorder, start offset seconds, duration seconds, items in, items out)
INSERT INTO runstages (id, tenantid, runid, stage, status, startedutc, completedutc, durationms, itemsin, itemsout, errormessage, sortorder, active, createdutc, lastupdateutc)
SELECT pg_temp.did('rst_', (v.r - 1) * 15 + v.ord + 1), 'default', pg_temp.did('run_', v.r), v.stage, 'Succeeded',
       r.startedutc + make_interval(secs => v.startsec), r.startedutc + make_interval(secs => v.startsec + v.dur), v.dur * 1000.0,
       v.itemsin, v.itemsout, NULL, v.ord, true, r.startedutc + make_interval(secs => v.startsec), r.startedutc + make_interval(secs => v.startsec + v.dur)
FROM (VALUES
  (1, 'AssembleContext', 0, 0.0, 0.4, 1, 1), (1, 'PlanQueries', 1, 0.4, 6.1, 1, 3), (1, 'Search', 2, 6.5, 4.2, 3, 14), (1, 'Filter', 3, 10.7, 0.3, 14, 6),
  (1, 'Fetch', 4, 11.0, 9.8, 6, 3), (1, 'Extract', 5, 20.8, 41.5, 3, 3), (1, 'Embed', 6, 62.3, 1.2, 3, 3), (1, 'Dedupe', 7, 63.5, 0.6, 3, 3),
  (1, 'Score', 8, 64.1, 38.7, 3, 3), (1, 'Synthesize', 9, 102.8, 17.9, 3, 1), (1, 'ReviewPredictions', 10, 120.7, 0.2, 0, 0), (1, 'Predict', 11, 120.9, 21.4, 3, 3),
  (1, 'Suggest', 12, 142.3, 16.0, 3, 2), (1, 'ComposeBriefing', 13, 158.3, 31.2, 3, 3), (1, 'Notify', 14, 189.5, 1.5, 1, 1),
  (2, 'AssembleContext', 0, 0.0, 0.5, 1, 1), (2, 'PlanQueries', 1, 0.5, 5.4, 1, 3), (2, 'Search', 2, 5.9, 3.8, 3, 16), (2, 'Filter', 3, 9.7, 0.3, 16, 7),
  (2, 'Fetch', 4, 10.0, 8.9, 7, 4), (2, 'Extract', 5, 18.9, 36.2, 4, 4), (2, 'Embed', 6, 55.1, 1.1, 4, 4), (2, 'Dedupe', 7, 56.2, 0.8, 4, 3),
  (2, 'Score', 8, 57.0, 30.5, 3, 3), (2, 'Synthesize', 9, 87.5, 15.2, 3, 1), (2, 'ReviewPredictions', 10, 102.7, 9.6, 2, 1), (2, 'Predict', 11, 112.3, 18.1, 3, 0),
  (2, 'Suggest', 12, 130.4, 12.7, 3, 1), (2, 'ComposeBriefing', 13, 143.1, 25.4, 3, 3), (2, 'Notify', 14, 168.5, 1.4, 1, 1),
  (3, 'AssembleContext', 0, 0.0, 0.4, 1, 1), (3, 'PlanQueries', 1, 0.4, 5.9, 1, 3), (3, 'Search', 2, 6.3, 4.6, 3, 12), (3, 'Filter', 3, 10.9, 0.2, 12, 6),
  (3, 'Fetch', 4, 11.1, 10.4, 6, 4), (3, 'Extract', 5, 21.5, 33.8, 4, 4), (3, 'Embed', 6, 55.3, 1.0, 4, 4), (3, 'Dedupe', 7, 56.3, 0.5, 4, 4),
  (3, 'Score', 8, 56.8, 29.9, 4, 4), (3, 'Synthesize', 9, 86.7, 14.6, 4, 1), (3, 'ReviewPredictions', 10, 101.3, 0.2, 0, 0), (3, 'Predict', 11, 101.5, 17.3, 4, 2),
  (3, 'Suggest', 12, 118.8, 13.1, 4, 2), (3, 'ComposeBriefing', 13, 131.9, 22.6, 4, 4), (3, 'Notify', 14, 154.5, 1.3, 1, 1)
) AS v(r, stage, ord, startsec, dur, itemsin, itemsout)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

-- Search queries ------------------------------------------------------------------------------------------------

INSERT INTO searchqueries (id, tenantid, runid, topicid, dimensionid, querytext, rationale, searchproviderid, provider, resultcount, durationms, succeeded, errormessage, active, createdutc, lastupdateutc)
SELECT pg_temp.did('qry_', v.n), 'default', pg_temp.did('run_', v.r), r.topicid, pg_temp.did('dim_', v.d), v.q, v.why, nullif(:'spv_searx', ''), 'Searxng',
       v.cnt, v.ms, true, NULL, true, r.startedutc + interval '7 seconds', r.startedutc + interval '9 seconds'
FROM (VALUES
  (1, 1, 1, 'coding assistant release repository-wide edits', 'New assistant capabilities your team could adopt.', 5, 1840.0),
  (2, 1, 2, 'agent framework SDK 1.0 typed tool calling', 'Agent SDK maturity for internal agents.', 5, 2110.0),
  (3, 1, 3, 'coding assistant seat pricing change 2026', 'Pricing changes for a 40-person team.', 4, 1630.0),
  (4, 2, 1, 'self-hosted coding model regulated teams survey', 'Answers the tracked question on self-hosted models.', 6, 1970.0),
  (5, 2, 2, 'Tessel SDK sandbox release', 'Follow-up on last run''s Tessel finding and prediction.', 5, 1540.0),
  (6, 2, 1, 'editor telemetry default change', 'Editor policy changes that affect configuration.', 5, 1720.0),
  (7, 3, 5, 'cold climate heat pump field test COP below zero', 'Performance data for your winters.', 4, 2250.0),
  (8, 3, 6, 'Washington 2027 heat pump rebate portal', 'Tracked question on rebate timing.', 4, 1880.0),
  (9, 3, 7, 'home battery backup sizing outage net metering', 'Practical solar and battery guidance.', 4, 2020.0)
) AS v(n, r, d, q, why, cnt, ms)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

-- Sources (fictional) -------------------------------------------------------------------------------------------

INSERT INTO sourceitems (id, tenantid, canonicalurl, canonicalurlhash, domain, title, publishedutc, fetchedutc, httpstatus, contenttype, contenthash,
  contenttext, contentbytes, firstseenrunid, timesseen, active, createdutc, lastupdateutc)
SELECT pg_temp.did('src_', v.n), 'default', 'https://' || v.path, pg_temp.sha(v.path), split_part(v.path, '/', 1), v.title, r.startedutc - make_interval(hours => v.agehours),
       r.startedutc + interval '15 seconds', 200, 'text/html', pg_temp.sha(v.body), v.body, octet_length(v.body), pg_temp.did('run_', v.r), 1, true,
       r.startedutc + interval '15 seconds', r.startedutc + interval '15 seconds'
FROM (VALUES
  (1, 1, 'devtools-weekly.example/lumen-code-4-2-repository-edits', 'Lumen Code 4.2 ships repository-wide edits', 20,
   'Lumen Code 4.2 is out today. The headline feature is repository-wide edits: the assistant now proposes a single reviewable change set that spans every file a refactor touches, instead of editing one file at a time. In our tests on a 300,000 line monorepo, a rename across 41 files completed in under two minutes with every change shown as a normal diff. Teams can require approval before any change set is applied. The release also adds a per-repository policy file that limits which directories the assistant may modify.'),
  (2, 1, 'agents-journal.example/tessel-sdk-1-0-typed-tools', 'Tessel agent SDK reaches 1.0 with typed tool calling', 30,
   'The Tessel agent SDK reached version 1.0 this week. Tool definitions are now generated from typed function signatures, so a mismatched argument fails at build time rather than in production. The maintainers say the 1.x line will keep the tool calling interface stable for at least a year. A sandboxed shell tool is on the roadmap but did not make the 1.0 cut. Version 1.0 supports four model providers and ships with a local trace viewer.'),
  (3, 1, 'devtools-weekly.example/quill-assistant-usage-seats', 'Quill Assistant moves to usage-based seats', 40,
   'Quill Assistant is replacing flat per-seat pricing with usage-based seats starting January 1. Each seat includes 2,000 assistant requests per month, and additional requests are billed at 2 cents each. Annual contracts signed before the change keep flat pricing until renewal. Quill says 80 percent of current users stay under the included allowance, but heavy users of agent mode will pay more.'),
  (4, 2, 'opensource-ledger.example/self-hosted-assistants-survey', 'Survey: 38 percent of regulated teams want self-hosted coding models', 10,
   'A survey of 1,200 engineering leaders found that 38 percent of teams in regulated industries will only adopt a coding assistant that can run against a self-hosted model. Among those teams, the most common blocker was source code leaving the network. Three of the five assistants in the survey now offer a self-hosted model option, up from one a year ago. Respondents ranked audit logging second after data residency.'),
  (5, 2, 'agents-journal.example/tessel-sdk-1-1-sandbox', 'Tessel SDK 1.1 adds a sandboxed shell tool', 6,
   'Tessel SDK 1.1 adds the sandboxed shell tool that was cut from 1.0. Commands run in an isolated container with no network access by default, and every command and its output is recorded in the trace. The release arrived 19 days after 1.0. Existing 1.0 tool definitions work unchanged.'),
  (6, 2, 'devtools-weekly.example/editor-telemetry-opt-in', 'Two editors make telemetry opt-in by default', 8,
   'Two popular code editors will make usage telemetry opt-in by default in their next releases. Administrators can still enable telemetry for a whole organization through a managed settings file. Neither change affects crash reporting.'),
  (7, 2, 'codecraft-news.example/reviews/lumen-code-4-2', 'Review: Lumen Code 4.2 and its repository-wide edits', 14,
   'We spent a week with Lumen Code 4.2. Repository-wide edits are the real story: the assistant proposes one change set across many files and shows each change as a diff. Large renames were reliable in our tests, though the first proposal on a mixed-language repository missed generated files.'),
  (8, 3, 'energy-desk.example/cold-climate-heat-pump-field-test', 'Field test: cold-climate heat pumps hold COP 2.1 at -20 F', 22,
   'A two-winter field test of 64 homes found that cold-climate heat pumps held an average coefficient of performance of 2.1 at -20 F. In plain terms, the units still delivered about twice as much heat as the electricity they used on the coldest nights. Homes with undersized units leaned on backup resistance heat 3 times as often. The researchers say a proper load calculation mattered more than the brand of heat pump.'),
  (9, 3, 'energy-desk.example/state-rebate-portal-2027', 'State says 2027 heat pump rebate portal opens in March', 12,
   'The state energy office says its 2027 heat pump rebate portal will open on March 2. Households under 150 percent of area median income can receive up to 8,000 dollars toward a heat pump, and other households up to 2,000 dollars. Contractors must be on the approved list for the installation to qualify. The office expects funds for the first year to run out within six months.'),
  (10, 3, 'homegrid-notes.example/guides/battery-backup-sizing', 'How to size a home battery for a two-day outage', 70,
   'Sizing a home battery starts with the loads you cannot live without. For most homes, a refrigerator, internet, lights, and a furnace fan need about 10 kWh per day. A two-day outage therefore needs roughly 20 kWh of storage, or less with rooftop solar recharging during the day.'),
  (11, 3, 'homegrid-notes.example/news/net-metering-proposal', 'Utility proposes net metering changes for new solar', 30,
   'The regional utility has proposed changing net metering for new rooftop solar customers from 2028. Exported electricity would be credited at a lower avoided-cost rate instead of the full retail rate. Existing customers would keep current terms for 20 years. Public comments are open until January 15.')
) AS v(n, r, path, title, agehours, body)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

-- Search results: every fetched source plus a few the filter skipped.
INSERT INTO searchresults (id, tenantid, searchqueryid, runid, topicid, url, canonicalurlhash, title, snippet, publishedutc, rank, provider, disposition, sourceitemid, active, createdutc, lastupdateutc)
SELECT pg_temp.did('srh_', v.n), 'default', pg_temp.did('qry_', v.q), pg_temp.did('run_', v.r), r.topicid, 'https://' || v.path, pg_temp.sha(v.path), v.title, v.snippet,
       r.startedutc - make_interval(hours => v.agehours), v.rnk, 'Searxng', v.disp, CASE WHEN v.src IS NULL THEN NULL ELSE pg_temp.did('src_', v.src) END, true,
       r.startedutc + interval '10 seconds', r.startedutc + interval '11 seconds'
FROM (VALUES
  (1, 1, 1, 'devtools-weekly.example/lumen-code-4-2-repository-edits', 'Lumen Code 4.2 ships repository-wide edits', 'The assistant now proposes a single reviewable change set...', 20, 1, 'Fetched', 1),
  (2, 1, 1, 'hype-aggregator.example/lumen-4-2-changes-everything', 'Lumen 4.2 changes everything', 'You will not believe what the new release can do...', 18, 2, 'SkippedBlocked', NULL),
  (3, 2, 1, 'agents-journal.example/tessel-sdk-1-0-typed-tools', 'Tessel agent SDK reaches 1.0 with typed tool calling', 'Tool definitions are now generated from typed function signatures...', 30, 1, 'Fetched', 2),
  (4, 3, 1, 'devtools-weekly.example/quill-assistant-usage-seats', 'Quill Assistant moves to usage-based seats', 'Each seat includes 2,000 assistant requests per month...', 40, 1, 'Fetched', 3),
  (5, 4, 2, 'opensource-ledger.example/self-hosted-assistants-survey', 'Survey: 38 percent of regulated teams want self-hosted coding models', 'A survey of 1,200 engineering leaders found...', 10, 1, 'Fetched', 4),
  (6, 5, 2, 'agents-journal.example/tessel-sdk-1-1-sandbox', 'Tessel SDK 1.1 adds a sandboxed shell tool', 'Commands run in an isolated container with no network access...', 6, 1, 'Fetched', 5),
  (7, 5, 2, 'agents-journal.example/tessel-sdk-1-0-typed-tools', 'Tessel agent SDK reaches 1.0 with typed tool calling', 'Tool definitions are now generated from typed function signatures...', 54, 2, 'SkippedSeen', 2),
  (8, 6, 2, 'devtools-weekly.example/editor-telemetry-opt-in', 'Two editors make telemetry opt-in by default', 'Administrators can still enable telemetry through a managed settings file...', 8, 1, 'Fetched', 6),
  (9, 4, 2, 'codecraft-news.example/reviews/lumen-code-4-2', 'Review: Lumen Code 4.2 and its repository-wide edits', 'We spent a week with Lumen Code 4.2...', 14, 3, 'Fetched', 7),
  (10, 7, 3, 'energy-desk.example/cold-climate-heat-pump-field-test', 'Field test: cold-climate heat pumps hold COP 2.1 at -20 F', 'A two-winter field test of 64 homes found...', 22, 1, 'Fetched', 8),
  (11, 8, 3, 'energy-desk.example/state-rebate-portal-2027', 'State says 2027 heat pump rebate portal opens in March', 'The state energy office says its 2027 portal will open on March 2...', 12, 1, 'Fetched', 9),
  (12, 9, 3, 'homegrid-notes.example/guides/battery-backup-sizing', 'How to size a home battery for a two-day outage', 'Sizing a home battery starts with the loads you cannot live without...', 70, 1, 'Fetched', 10),
  (13, 9, 3, 'homegrid-notes.example/news/net-metering-proposal', 'Utility proposes net metering changes for new solar', 'Exported electricity would be credited at a lower avoided-cost rate...', 30, 2, 'Fetched', 11),
  (14, 7, 3, 'hvac-pro-supply.example/commercial-rooftop-units', 'Commercial rooftop units for 2027', 'Our full line of commercial HVAC equipment...', 90, 3, 'SkippedLimit', NULL)
) AS v(n, q, r, path, title, snippet, agehours, rnk, disp, src)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

-- Findings ------------------------------------------------------------------------------------------------------

INSERT INTO findings (id, tenantid, runid, topicid, userid, dimensionid, sourceitemid, title, summary, whyitmatters, category, relevancescore, llmscore,
  feedbackscore, noveltyscore, sourcescore, recencyscore, tier, relevancerationale, confidence, isduplicate, duplicateoffindingid, isupdateoffindingid,
  rank, userrating, publishedutc, isdryrun, active, createdutc, lastupdateutc, pinned, hidden)
SELECT pg_temp.did('fnd_', v.n), 'default', pg_temp.did('run_', v.r), r.topicid, :'uid', pg_temp.did('dim_', v.d), pg_temp.did('src_', v.src), v.title, v.summary, v.why,
       v.cat, v.score, v.llm, v.fb, v.nov, v.srcscore, v.rec, v.tier, v.rationale, v.conf, v.dup IS NOT NULL,
       CASE WHEN v.dup IS NULL THEN NULL ELSE pg_temp.did('fnd_', v.dup) END, CASE WHEN v.upd IS NULL THEN NULL ELSE pg_temp.did('fnd_', v.upd) END,
       v.rnk, NULL, s.publishedutc, false, true, r.startedutc + interval '100 seconds', r.startedutc + interval '100 seconds', v.pin, false
FROM (VALUES
  (1, 1, 1, 1, 'Lumen Code 4.2 ships repository-wide edits',
   'The assistant now proposes one reviewable change set across every file a refactor touches, with optional approval before anything is applied.',
   'Cross-file refactors are where your team spends the most review time, and the per-repository policy file fits your approval rules.',
   'Release', 86.0, 90.0, 60.0, 92.0, 80.0, 88.0, 'MustRead', 'A capability change your team can adopt directly, from a preferred source.', 0.85, NULL, NULL, 1, true),
  (2, 1, 2, 2, 'Tessel agent SDK reaches 1.0 with typed tool calling',
   'Tool definitions are generated from typed signatures, and the 1.x tool interface is promised stable for a year.',
   'A stable interface lowers the risk of building your internal release bot on it.',
   'Release', 71.0, 74.0, 50.0, 88.0, 65.0, 80.0, 'WorthALook', 'Relevant SDK milestone; no sandbox yet.', 0.75, NULL, NULL, 2, false),
  (3, 1, 3, 3, 'Quill Assistant moves to usage-based seats',
   'From January 1 each seat includes 2,000 requests a month, with extra requests at 2 cents; annual contracts keep flat pricing until renewal.',
   'Heavy agent-mode users on your team could raise the monthly bill.',
   'News', 64.0, 68.0, 50.0, 85.0, 80.0, 72.0, 'WorthALook', 'Direct budget impact for a team your size.', 0.7, NULL, NULL, 3, false),
  (4, 2, 1, 4, 'Survey: 38 percent of regulated teams want self-hosted coding models',
   'Among regulated teams, keeping source code on the network is the top blocker; three of five assistants now offer a self-hosted option.',
   'This answers your tracked question and narrows the shortlist for regulated repositories.',
   'Data', 83.0, 86.0, 55.0, 90.0, 70.0, 92.0, 'MustRead', 'Answers a tracked question with survey data.', 0.8, NULL, NULL, 1, false),
  (5, 2, 2, 5, 'Tessel SDK 1.1 adds a sandboxed shell tool',
   'The shell tool runs commands in an isolated container with no network by default and records every command in the trace.',
   'The missing piece for running build commands from an internal agent safely.',
   'Release', 68.0, 72.0, 50.0, 70.0, 65.0, 95.0, 'WorthALook', 'Update to an earlier finding; resolves a prediction.', 0.75, NULL, 2, 2, false),
  (6, 2, 1, 6, 'Two editors make telemetry opt-in by default',
   'Usage telemetry becomes opt-in in the next releases of two editors; organizations can still enable it with a managed settings file.',
   'Minor configuration change if you rely on editor usage data.',
   'News', 52.0, 55.0, 30.0, 80.0, 80.0, 90.0, 'Fyi', 'Low impact policy change.', 0.6, NULL, NULL, 3, false),
  (7, 2, 1, 7, 'Review: Lumen Code 4.2 and its repository-wide edits',
   'A hands-on review of the same release, noting that the first proposal missed generated files.',
   NULL,
   'Opinion', 40.0, 62.0, 50.0, 10.0, 60.0, 85.0, 'BelowThreshold', 'Duplicate of an earlier finding.', 0.5, 1, NULL, 0, false),
  (8, 3, 5, 8, 'Field test: cold-climate heat pumps hold COP 2.1 at -20 F',
   'Across 64 homes and two winters, units still delivered about twice the heat of the electricity they used on the coldest nights.',
   'Your coldest nights are well above -20 F, so a correctly sized unit should not need gas backup.',
   'Research', 88.0, 92.0, 70.0, 90.0, 85.0, 86.0, 'MustRead', 'Primary performance data for your climate.', 0.85, NULL, NULL, 1, true),
  (9, 3, 6, 9, 'State says 2027 heat pump rebate portal opens in March',
   'The portal opens March 2 with up to 8,000 dollars for income-qualified households and 2,000 dollars for others; installers must be on the approved list.',
   'Answers your tracked question and sets the timeline for your furnace replacement.',
   'News', 81.0, 84.0, 50.0, 85.0, 85.0, 90.0, 'MustRead', 'Answers a tracked question from a preferred source.', 0.8, NULL, NULL, 2, false),
  (10, 3, 7, 10, 'How to size a home battery for a two-day outage',
   'Essential loads use about 10 kWh a day, so a two-day outage needs roughly 20 kWh, or less with solar recharging.',
   'A rule of thumb for comparing battery quotes.',
   'Tutorial', 55.0, 58.0, 40.0, 75.0, 60.0, 40.0, 'Fyi', 'General guidance, not news.', 0.6, NULL, NULL, 4, false),
  (11, 3, 7, 11, 'Utility proposes net metering changes for new solar',
   'New rooftop solar customers from 2028 would be credited at an avoided-cost rate; existing customers keep current terms for 20 years.',
   'Installing before 2028 could lock in better terms if you add solar later.',
   'News', 63.0, 66.0, 50.0, 80.0, 70.0, 75.0, 'WorthALook', 'Policy change with a deadline.', 0.7, NULL, NULL, 3, false)
) AS v(n, r, d, src, title, summary, why, cat, score, llm, fb, nov, srcscore, rec, tier, rationale, conf, dup, upd, rnk, pin)
JOIN runs r ON r.id = pg_temp.did('run_', v.r)
JOIN sourceitems s ON s.id = pg_temp.did('src_', v.src);

-- Evidence: verified quotes, with offsets computed from the stored source text.
INSERT INTO evidence (id, tenantid, sourceitemid, runid, quote, startoffset, endoffset, contexttext, quotehash, verified, active, createdutc, lastupdateutc)
SELECT pg_temp.did('evd_', v.n), 'default', s.id, pg_temp.did('run_', v.r), v.q, strpos(s.contenttext, v.q) - 1, strpos(s.contenttext, v.q) - 1 + length(v.q),
       substr(s.contenttext, greatest(1, strpos(s.contenttext, v.q) - 80), length(v.q) + 160), pg_temp.sha(lower(v.q)), true, true,
       s.createdutc + interval '20 seconds', s.createdutc + interval '20 seconds'
FROM (VALUES
  (1, 1, 1, 'the assistant now proposes a single reviewable change set that spans every file a refactor touches'),
  (2, 1, 1, 'Teams can require approval before any change set is applied.'),
  (3, 1, 2, 'Tool definitions are now generated from typed function signatures'),
  (4, 1, 3, 'Each seat includes 2,000 assistant requests per month, and additional requests are billed at 2 cents each.'),
  (5, 2, 4, '38 percent of teams in regulated industries will only adopt a coding assistant that can run against a self-hosted model'),
  (6, 2, 4, 'Three of the five assistants in the survey now offer a self-hosted model option'),
  (7, 2, 5, 'Commands run in an isolated container with no network access by default'),
  (8, 2, 6, 'will make usage telemetry opt-in by default in their next releases'),
  (9, 2, 7, 'Repository-wide edits are the real story'),
  (10, 3, 8, 'held an average coefficient of performance of 2.1 at -20 F'),
  (11, 3, 8, 'a proper load calculation mattered more than the brand of heat pump'),
  (12, 3, 9, 'its 2027 heat pump rebate portal will open on March 2'),
  (13, 3, 10, 'A two-day outage therefore needs roughly 20 kWh of storage'),
  (14, 3, 11, 'Existing customers would keep current terms for 20 years.')
) AS v(n, r, src, q)
JOIN sourceitems s ON s.id = pg_temp.did('src_', v.src);

INSERT INTO findingevidence (id, tenantid, findingid, evidenceid, role, sortorder, active, createdutc, lastupdateutc)
SELECT pg_temp.did('fev_', v.n), 'default', pg_temp.did('fnd_', v.f), pg_temp.did('evd_', v.e), v.role, v.ord, true, f.createdutc, f.createdutc
FROM (VALUES
  (1, 1, 1, 'Supports', 0), (2, 1, 2, 'Supports', 1), (3, 2, 3, 'Supports', 0), (4, 3, 4, 'Supports', 0), (5, 4, 5, 'Supports', 0),
  (6, 4, 6, 'Context', 1), (7, 5, 7, 'Supports', 0), (8, 6, 8, 'Supports', 0), (9, 7, 9, 'Supports', 0), (10, 8, 10, 'Supports', 0),
  (11, 8, 11, 'Context', 1), (12, 9, 12, 'Supports', 0), (13, 10, 13, 'Supports', 0), (14, 11, 14, 'Supports', 0)
) AS v(n, f, e, role, ord)
JOIN findings f ON f.id = pg_temp.did('fnd_', v.f);

-- Predictions, review, suggestions ------------------------------------------------------------------------------

INSERT INTO predictions (id, tenantid, runid, topicid, userid, dimensionid, statement, rationale, confidence, horizonutc, status, resolvedutc, resolvedby,
  resolutionnote, brierscore, isdryrun, active, createdutc, lastupdateutc)
SELECT pg_temp.did('prd_', v.n), 'default', pg_temp.did('run_', v.r), r.topicid, :'uid', pg_temp.did('dim_', v.d), v.statement, v.rationale, v.conf,
       now() + make_interval(days => v.days), v.status, NULL, NULL, NULL, NULL, false, true, r.startedutc + interval '140 seconds', r.startedutc + interval '140 seconds'
FROM (VALUES
  (1, 1, 1, 'At least two more coding assistants will add a self-hosted model option before the end of March 2027.',
   'Regulated buyers are asking for it and one vendor shipped it this year.', 0.65, 130, 'Open'),
  (2, 1, 3, 'Quill Assistant will raise the included request allowance within 60 days of the pricing change.',
   'Usage-based seat changes often draw pushback from heavy users.', 0.35, 95, 'Open'),
  (3, 1, 2, 'Tessel will ship a sandboxed shell tool within one month of 1.0.',
   'The maintainers called it the top roadmap item and it nearly made 1.0.', 0.6, 10, 'ProposedResolved'),
  (4, 3, 6, 'The state heat pump rebate portal will open on schedule in March 2027.',
   'The office has published a date and the funding is already appropriated.', 0.7, 130, 'Open'),
  (5, 3, 5, 'List prices for cold-climate heat pumps under 4 tons will fall at least 10 percent by October 2027.',
   'Field results are driving volume and several manufacturers are expanding lines.', 0.4, 330, 'Open')
) AS v(n, r, d, statement, rationale, conf, days, status)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

INSERT INTO predictionreviews (id, tenantid, predictionid, runid, proposedstatus, rationale, confidence, acceptedbyuser, findingids, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('prv_', 1), 'default', pg_temp.did('prd_', 3), pg_temp.did('run_', 2), 'Accurate',
   'Tessel SDK 1.1 added the sandboxed shell tool 19 days after 1.0.', 0.9, NULL, ARRAY[pg_temp.did('fnd_', 5)], true,
   :t_a2 + interval '110 seconds', :t_a2 + interval '110 seconds');

INSERT INTO suggestions (id, tenantid, runid, topicid, userid, title, description, rationale, priority, effort, status, snoozeduntilutc, statuschangedutc,
  findingids, isdryrun, active, createdutc, lastupdateutc)
SELECT pg_temp.did('sug_', v.n), 'default', pg_temp.did('run_', v.r), r.topicid, :'uid', v.title, v.descr, v.rationale, v.priority, v.effort, v.status, NULL,
       CASE WHEN v.status = 'New' THEN NULL ELSE now() - interval '20 hours' END, ARRAY(SELECT pg_temp.did('fnd_', x) FROM unnest(v.f) AS x), false, true,
       r.startedutc + interval '150 seconds', r.startedutc + interval '150 seconds'
FROM (VALUES
  (1, 2, 'Pilot a self-hosted assistant on one regulated repository', 'Pick the repository with the strictest data rules and trial one of the three assistants with a self-hosted option for two weeks.',
   'The survey shows the option is now common, and it unblocks your regulated teams.', 'High', 'Days', 'New', ARRAY[4]),
  (2, 1, 'Re-forecast assistant spend under usage-based seats', 'Pull last quarter''s request counts per engineer and price them under the new allowance.',
   'Agent-mode users may exceed 2,000 requests a month.', 'Medium', 'Hours', 'Accepted', ARRAY[3]),
  (3, 1, 'Try typed tool calling on the release bot', 'Port two release bot tools to Tessel 1.0 typed definitions.', 'Catches argument mismatches at build time.', 'Low', 'Hours', 'Done', ARRAY[2]),
  (4, 3, 'Book a load calculation before the rebate portal opens', 'Ask two approved contractors for a Manual J load calculation and quotes.',
   'Correct sizing mattered most in the field test, and rebates require an approved installer.', 'High', 'Hours', 'New', ARRAY[8, 9]),
  (5, 3, 'Comment on the net metering proposal', 'Comments are open until January 15.', 'The change affects anyone adding solar after 2027.', 'Low', 'Minutes', 'New', ARRAY[11])
) AS v(n, r, title, descr, rationale, priority, effort, status, f)
JOIN runs r ON r.id = pg_temp.did('run_', v.r);

-- Briefings -----------------------------------------------------------------------------------------------------
-- brf 1: the Morning News edition (both topics). brf 2 to 4: the run briefing for each run. Markdown is assembled from the
-- sections below so the stored document and its sections always agree.

INSERT INTO briefings (id, tenantid, userid, kind, editionid, topicid, runid, researchrequestid, scheduleid, periodstartutc, periodendutc, title, markdown, status,
  composeafterutc, researchdeadlineutc, lockeduntilutc, briefingtemplateversionid, mediaassetid, markdownartifactid, htmlartifactid, itemcount, wordcount,
  readminutes, modelfallbacks, errormessage, pinned, isdryrun, composedutc, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('brf_', 1), 'default', :'uid', 'Edition', :'edn', NULL, NULL, NULL, nullif(:'edn_sch', ''), :t_edition - interval '24 hours', :t_edition,
   'Morning News for ' || :'today_long', NULL, 'Ready', :t_edition, :t_edition - interval '30 minutes', NULL, nullif(:'tpl_morning_v', ''), NULL, NULL, NULL,
   7, 0, 1, 0, NULL, false, false, :t_edition + interval '40 seconds', true, :t_edition, :t_edition + interval '40 seconds'),
  (pg_temp.did('brf_', 2), 'default', :'uid', 'Run', NULL, pg_temp.did('top_', 1), pg_temp.did('run_', 1), NULL, NULL, :t_a1 - interval '24 hours', :t_a1 + interval '160 seconds',
   'AI developer tools: ' || :'yesterday_long', NULL, 'Ready', NULL, NULL, NULL, nullif(:'tpl_run_v', ''), NULL, NULL, NULL,
   3, 0, 1, 0, NULL, false, false, :t_a1 + interval '189 seconds', true, :t_a1 + interval '158 seconds', :t_a1 + interval '189 seconds'),
  (pg_temp.did('brf_', 3), 'default', :'uid', 'Run', NULL, pg_temp.did('top_', 1), pg_temp.did('run_', 2), NULL, NULL, :t_a1 + interval '160 seconds', :t_a2 + interval '143 seconds',
   'AI developer tools: ' || :'today_long', NULL, 'Ready', NULL, NULL, NULL, nullif(:'tpl_run_v', ''), NULL, NULL, NULL,
   3, 0, 1, 0, NULL, false, false, :t_a2 + interval '168 seconds', true, :t_a2 + interval '143 seconds', :t_a2 + interval '168 seconds'),
  (pg_temp.did('brf_', 4), 'default', :'uid', 'Run', NULL, pg_temp.did('top_', 2), pg_temp.did('run_', 3), NULL, NULL, :t_b1 - interval '7 days', :t_b1 + interval '131 seconds',
   'Home energy and heat pumps: ' || :'today_long', NULL, 'Ready', NULL, NULL, NULL, nullif(:'tpl_run_v', ''), NULL, NULL, NULL,
   4, 0, 1, 0, NULL, false, false, :t_b1 + interval '154 seconds', true, :t_b1 + interval '131 seconds', :t_b1 + interval '154 seconds');

INSERT INTO briefingsections (id, tenantid, briefingid, sectionkey, heading, markdown, source, sortorder, active, createdutc, lastupdateutc)
SELECT pg_temp.did('bsc_', v.n), 'default', pg_temp.did('brf_', v.b), v.k, CASE WHEN v.k = 'opening' THEN b.title ELSE v.heading END, v.md, v.src, v.ord, true, b.composedutc, b.composedutc
FROM (VALUES
  -- Morning News edition
  (1, 1, 'opening', NULL, 'opening', 0,
   'The most important development across your topics: a two-winter field test shows cold-climate heat pumps still deliver about twice the heat of the electricity they use at -20 F [1], and the state rebate portal now has an opening date [4]. On the tools side, Lumen Code 4.2 brings repository-wide edits [2] and a new survey answers your question on self-hosted models [3].'),
  (2, 1, 'top-of-the-news', 'Top of the news', 'findings', 1,
   E'**Field test: cold-climate heat pumps hold COP 2.1 at -20 F** [1]. Across 64 homes and two winters, the units kept working on the coldest nights. Undersized systems leaned on backup heat three times as often, so sizing matters more than the brand you pick for your furnace replacement.\n\n**Lumen Code 4.2 ships repository-wide edits** [2]. One reviewable change set now covers every file a refactor touches, with an approval gate and a per-repository policy file that fit how your team reviews changes.\n\n**38 percent of regulated teams want self-hosted coding models** [3]. Three of the five assistants surveyed now offer that option, which answers your tracked question and shortens the list for your regulated repositories.\n\n**The 2027 heat pump rebate portal opens March 2** [4]. Up to 8,000 dollars for income-qualified households and 2,000 dollars for others, and the installer must be on the approved list.'),
  (3, 1, 'by-topic', 'By topic', 'findings', 2,
   E'### AI developer tools\n\nTessel SDK 1.1 adds the sandboxed shell tool that missed 1.0 [5]. Commands run with no network by default and land in the trace.\n\nQuill Assistant moves to usage-based seats on January 1 [6]. Agent-mode heavy users may cost more than today.\n\n### Home energy and heat pumps\n\nThe utility proposes lower credits for new rooftop solar from 2028 [7]. Existing customers keep current terms for 20 years.'),
  (4, 1, 'where-this-might-go', 'Where this might go', 'predictions', 3,
   E'- At least two more coding assistants add a self-hosted option before the end of March 2027 (65 percent). A vendor reversing course on data residency would change this.\n- The rebate portal opens on schedule in March (70 percent). Watch for a delayed rules package.\n- Cold-climate heat pump list prices under 4 tons fall 10 percent by October 2027 (40 percent). A supply shortage would push the other way.'),
  (5, 1, 'predictions-to-grade', 'Predictions to grade', 'predictions', 4,
   '- Tessel will ship a sandboxed shell tool within one month of 1.0 (60 percent). It shipped in 1.1, 19 days later. The research agent proposes marking it accurate; confirm or reject it on the Predictions page.'),
  (6, 1, 'dig-into-next', 'Dig into next', 'suggestions', 5,
   E'- **Book a load calculation before the rebate portal opens.** Sizing drove the field test results, and rebates need an approved installer.\n- **Pilot a self-hosted assistant on one regulated repository.** The option is now common enough to trial.'),
  (7, 1, 'sources', 'Sources', 'citations', 6, NULL),
  -- Run A1
  (8, 2, 'opening', NULL, 'opening', 0, 'The most important development: **Lumen Code 4.2 ships repository-wide edits** [1]. Tessel reached 1.0 and Quill changes its seat pricing in January.'),
  (9, 2, 'what-is-new', 'What is new', 'findings', 1,
   E'- **Lumen Code 4.2 ships repository-wide edits** [1]. One reviewable change set spans every file a refactor touches.\n- **Tessel agent SDK reaches 1.0 with typed tool calling** [2]. The 1.x tool interface is promised stable for a year.\n- **Quill Assistant moves to usage-based seats** [3]. 2,000 requests per seat per month, then 2 cents each.'),
  (10, 2, 'where-this-might-go', 'Where this might go', 'predictions', 2,
   E'- At least two more coding assistants add a self-hosted option before the end of March 2027 (65 percent).\n- Quill raises the included allowance within 60 days (35 percent).\n- Tessel ships a sandboxed shell tool within a month (60 percent).'),
  (11, 2, 'dig-into-next', 'Dig into next', 'suggestions', 3,
   E'- **Re-forecast assistant spend under usage-based seats.** Agent-mode users may exceed the allowance.\n- **Try typed tool calling on the release bot.** Catch argument mismatches at build time.'),
  (12, 2, 'sources', 'Sources', 'citations', 4, NULL),
  -- Run A2
  (13, 3, 'opening', NULL, 'opening', 0, 'The most important development: **38 percent of regulated teams want self-hosted coding models** [1], which answers your tracked question.'),
  (14, 3, 'what-is-new', 'What is new', 'findings', 1,
   E'- **Survey: 38 percent of regulated teams want self-hosted coding models** [1]. Three of five assistants now offer it.\n- **Tessel SDK 1.1 adds a sandboxed shell tool** [2]. An update to last run''s Tessel finding.\n- **Two editors make telemetry opt-in by default** [3]. Organizations can still enable it centrally.'),
  (15, 3, 'dig-into-next', 'Dig into next', 'suggestions', 2,
   '- **Pilot a self-hosted assistant on one regulated repository.** Two weeks on the strictest repository.'),
  (16, 3, 'sources', 'Sources', 'citations', 3, NULL),
  -- Run B1
  (17, 4, 'opening', NULL, 'opening', 0, 'The most important development: **cold-climate heat pumps held a COP of 2.1 at -20 F** in a two-winter field test [1].'),
  (18, 4, 'what-is-new', 'What is new', 'findings', 1,
   E'- **Field test: cold-climate heat pumps hold COP 2.1 at -20 F** [1]. COP (coefficient of performance) is heat delivered per unit of electricity.\n- **State says 2027 heat pump rebate portal opens in March** [2]. Up to 8,000 dollars for income-qualified households.\n- **Utility proposes net metering changes for new solar** [3]. New customers from 2028 get lower export credits.\n- **How to size a home battery for a two-day outage** [4]. Roughly 20 kWh for essential loads.'),
  (19, 4, 'where-this-might-go', 'Where this might go', 'predictions', 2,
   E'- The rebate portal opens on schedule in March 2027 (70 percent).\n- Cold-climate heat pump list prices under 4 tons fall 10 percent by October 2027 (40 percent).'),
  (20, 4, 'dig-into-next', 'Dig into next', 'suggestions', 3,
   E'- **Book a load calculation before the rebate portal opens.**\n- **Comment on the net metering proposal** before January 15.'),
  (21, 4, 'sources', 'Sources', 'citations', 4, NULL)
) AS v(n, b, k, heading, src, ord, md)
JOIN briefings b ON b.id = pg_temp.did('brf_', v.b);

-- (item, briefing, section, type, item number, citation, order)
INSERT INTO briefingitems (id, tenantid, briefingid, sectionid, itemtype, itemid, citationnumber, sortorder, active, createdutc, lastupdateutc)
SELECT pg_temp.did('bit_', v.n), 'default', pg_temp.did('brf_', v.b), pg_temp.did('bsc_', v.s), v.t,
       pg_temp.did(CASE v.t WHEN 'Finding' THEN 'fnd_' WHEN 'Prediction' THEN 'prd_' ELSE 'sug_' END, v.i), v.c, v.ord, true, b.composedutc, b.composedutc
FROM (VALUES
  (1, 1, 2, 'Finding', 8, 1, 0), (2, 1, 2, 'Finding', 1, 2, 1), (3, 1, 2, 'Finding', 4, 3, 2), (4, 1, 2, 'Finding', 9, 4, 3),
  (5, 1, 3, 'Finding', 5, 5, 0), (6, 1, 3, 'Finding', 3, 6, 1), (7, 1, 3, 'Finding', 11, 7, 2),
  (8, 1, 4, 'Prediction', 1, NULL, 0), (9, 1, 4, 'Prediction', 4, NULL, 1), (10, 1, 4, 'Prediction', 5, NULL, 2),
  (11, 1, 5, 'Prediction', 3, NULL, 0), (12, 1, 6, 'Suggestion', 4, NULL, 0), (13, 1, 6, 'Suggestion', 1, NULL, 1),
  (14, 2, 9, 'Finding', 1, 1, 0), (15, 2, 9, 'Finding', 2, 2, 1), (16, 2, 9, 'Finding', 3, 3, 2),
  (17, 2, 10, 'Prediction', 1, NULL, 0), (18, 2, 10, 'Prediction', 2, NULL, 1), (19, 2, 10, 'Prediction', 3, NULL, 2),
  (20, 2, 11, 'Suggestion', 2, NULL, 0), (21, 2, 11, 'Suggestion', 3, NULL, 1),
  (22, 3, 14, 'Finding', 4, 1, 0), (23, 3, 14, 'Finding', 5, 2, 1), (24, 3, 14, 'Finding', 6, 3, 2), (25, 3, 15, 'Suggestion', 1, NULL, 0),
  (26, 4, 18, 'Finding', 8, 1, 0), (27, 4, 18, 'Finding', 9, 2, 1), (28, 4, 18, 'Finding', 11, 3, 2), (29, 4, 18, 'Finding', 10, 4, 3),
  (30, 4, 19, 'Prediction', 4, NULL, 0), (31, 4, 19, 'Prediction', 5, NULL, 1), (32, 4, 20, 'Suggestion', 4, NULL, 0), (33, 4, 20, 'Suggestion', 5, NULL, 1)
) AS v(n, b, s, t, i, c, ord)
JOIN briefings b ON b.id = pg_temp.did('brf_', v.b);

-- Sources sections list the cited sources in citation order, in the composer's format.
UPDATE briefingsections bs SET markdown = c.md
FROM (
  SELECT bi.briefingid, string_agg(bi.citationnumber || '. [' || s.title || '](' || s.canonicalurl || '), ' || s.domain || ', ' || to_char(s.publishedutc, 'Mon FMDD, YYYY'),
                                   E'\n' ORDER BY bi.citationnumber) AS md
  FROM briefingitems bi JOIN findings f ON f.id = bi.itemid JOIN sourceitems s ON s.id = f.sourceitemid
  WHERE bi.itemtype = 'Finding' AND bi.citationnumber IS NOT NULL AND bi.id LIKE 'bit_mmndemo0_%'
  GROUP BY bi.briefingid
) c
WHERE bs.briefingid = c.briefingid AND bs.sectionkey = 'sources';

-- Assemble each briefing's Markdown from its sections, then word count and reading time.
UPDATE briefings b SET markdown = m.md
FROM (
  SELECT bs.briefingid,
         '# ' || max(CASE WHEN bs.sectionkey = 'opening' THEN bs.heading END) || E'\n\n' || max(CASE WHEN bs.sectionkey = 'opening' THEN bs.markdown END) || E'\n\n' ||
         string_agg('## ' || bs.heading || E'\n\n' || bs.markdown, E'\n\n' ORDER BY bs.sortorder) FILTER (WHERE bs.sectionkey <> 'opening') ||
         E'\n\n_Demo content: the sources and products in this briefing are fictional._\n' AS md
  FROM briefingsections bs WHERE bs.id LIKE 'bsc_mmndemo0_%'
  GROUP BY bs.briefingid
) m
WHERE b.id = m.briefingid;

UPDATE briefings SET wordcount = coalesce(array_length(regexp_split_to_array(trim(regexp_replace(markdown, '[#*_\[\]()]', ' ', 'g')), '\s+'), 1), 0)
WHERE id LIKE 'brf_mmndemo0_%';
UPDATE briefings SET readminutes = greatest(1, ceil(wordcount / 220.0)::integer) WHERE id LIKE 'brf_mmndemo0_%';

-- Feedback, reason tags, learned preferences --------------------------------------------------------------------

INSERT INTO feedback (id, tenantid, userid, targettype, targetid, topicid, relevance, accuracy, usefulness, reason, channel, emailmessageid, supersedesfeedbackid,
  iscurrent, active, createdutc, lastupdateutc)
SELECT pg_temp.did('fbk_', v.n), 'default', :'uid', v.tt,
       pg_temp.did(CASE v.tt WHEN 'Finding' THEN 'fnd_' WHEN 'Suggestion' THEN 'sug_' WHEN 'Briefing' THEN 'brf_' ELSE 'prd_' END, v.target),
       CASE WHEN v.topic IS NULL THEN NULL ELSE pg_temp.did('top_', v.topic) END, v.rel, NULL, v.useful, v.reason, v.channel, NULL, NULL, true, true,
       now() - make_interval(mins => v.agomin), now() - make_interval(mins => v.agomin)
FROM (VALUES
  (1, 'Finding', 1, 1, 'Relevant', NULL, 'Exactly the kind of release our team needs to evaluate.', 'Email', 1500),
  (2, 'Finding', 3, 1, 'SomewhatRelevant', NULL, 'We are on annual seats until June, so this can wait.', 'Dashboard', 1440),
  (3, 'Finding', 6, 1, 'NotRelevant', NULL, 'Telemetry policy is handled by security, not by my team.', 'Dashboard', 120),
  (4, 'Finding', 4, 1, 'Relevant', NULL, NULL, 'Mobile', 110),
  (5, 'Suggestion', 3, 1, NULL, 'Useful', 'Ported two tools in an afternoon.', 'Dashboard', 1200),
  (6, 'Finding', 8, 2, 'Relevant', NULL, 'This settles the gas backup question for us.', 'Mobile', 100),
  (7, 'Finding', 10, 2, 'NotRelevant', NULL, 'We already have a battery; skip buying guides.', 'Email', 95),
  (8, 'Briefing', 1, NULL, 'Relevant', NULL, 'Good length for the commute.', 'Email', 90)
) AS v(n, tt, target, topic, rel, useful, reason, channel, agomin);

INSERT INTO feedbackreasontags (id, tenantid, feedbackid, topicid, tag, active, createdutc, lastupdateutc)
SELECT pg_temp.did('frt_', v.n), 'default', pg_temp.did('fbk_', v.f), pg_temp.did('top_', v.topic), v.tag, true, now() - interval '80 minutes', now() - interval '80 minutes'
FROM (VALUES
  (1, 2, 1, 'timing'), (2, 3, 1, 'not-my-role'), (3, 3, 1, 'policy-news'), (4, 7, 2, 'already-owned'), (5, 7, 2, 'buying-guide')
) AS v(n, f, topic, tag);

INSERT INTO learnedpreferences (id, tenantid, userid, topicid, kind, statement, status, origin, supportcount, confidence, lastevidenceutc, decidedutc, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('lrn_', 1), 'default', :'uid', pg_temp.did('top_', 1), 'Avoid', 'Editor and assistant telemetry policy news unless it changes what engineers must configure',
   'Proposed', 'Distilled', 1, 0.62, now() - interval '120 minutes', NULL, true, now() - interval '75 minutes', now() - interval '75 minutes'),
  (pg_temp.did('lrn_', 2), 'default', :'uid', pg_temp.did('top_', 2), 'Avoid', 'Home battery buying guides; the reader already owns a battery',
   'Proposed', 'Distilled', 1, 0.7, now() - interval '95 minutes', NULL, true, now() - interval '75 minutes', now() - interval '75 minutes'),
  (pg_temp.did('lrn_', 3), 'default', :'uid', pg_temp.did('top_', 1), 'Prefer', 'Releases and data the team can act on this quarter over vendor commentary',
   'Active', 'Distilled', 2, 0.74, now() - interval '110 minutes', now() - interval '60 minutes', true, now() - interval '18 hours', now() - interval '60 minutes'),
  (pg_temp.did('lrn_', 4), 'default', :'uid', NULL, 'StyleNote', 'Keep editions short enough to read on a 15-minute commute',
   'Active', 'User', 1, 1.0, now() - interval '90 minutes', now() - interval '85 minutes', true, now() - interval '85 minutes', now() - interval '85 minutes');

INSERT INTO learnedpreferencefeedback (id, tenantid, learnedpreferenceid, feedbackid, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('lpf_', 1), 'default', pg_temp.did('lrn_', 1), pg_temp.did('fbk_', 3), true, now() - interval '75 minutes', now() - interval '75 minutes'),
  (pg_temp.did('lpf_', 2), 'default', pg_temp.did('lrn_', 2), pg_temp.did('fbk_', 7), true, now() - interval '75 minutes', now() - interval '75 minutes'),
  (pg_temp.did('lpf_', 3), 'default', pg_temp.did('lrn_', 3), pg_temp.did('fbk_', 1), true, now() - interval '18 hours', now() - interval '18 hours'),
  (pg_temp.did('lpf_', 4), 'default', pg_temp.did('lrn_', 3), pg_temp.did('fbk_', 4), true, now() - interval '60 minutes', now() - interval '60 minutes'),
  (pg_temp.did('lpf_', 5), 'default', pg_temp.did('lrn_', 4), pg_temp.did('fbk_', 8), true, now() - interval '85 minutes', now() - interval '85 minutes');

-- The seeded feedback has already been learned from, so the learning worker does not call a model for it.
INSERT INTO learningcheckpoints (id, tenantid, userid, topicid, lastfeedbackutc, lastrunutc, proposed, active, createdutc, lastupdateutc)
VALUES
  (pg_temp.did('lcp_', 1), 'default', :'uid', pg_temp.did('top_', 1), now(), now() - interval '75 minutes', 1, true, now(), now()),
  (pg_temp.did('lcp_', 2), 'default', :'uid', pg_temp.did('top_', 2), now(), now() - interval '75 minutes', 1, true, now(), now());

\echo '[seed-data] done: 2 topics, the Morning News edition, 2 paused schedules, 3 runs, 11 sources, 11 findings, 5 predictions, 5 suggestions, 4 briefings, 8 feedback, 4 learned preferences.'
