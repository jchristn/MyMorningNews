// Fixtures for the Today, Briefings, Editions, and Templates tests.
export const MARKDOWN = [
  '# Morning News for November 19, 2026',
  '',
  'The most important development: **Agents ship**. [1]',
  '',
  '## Top of the news',
  '',
  '- **Agents ship** [1]: Coding agents reached general availability.',
  '- **Evals improve** [2]: A new benchmark landed.',
  '',
  '## Sources',
  '',
  '1. [Agents ship](https://example.com/a), example.com, Nov 18, 2026',
  '2. [Evals improve](https://example.com/b), example.com',
  ''
].join('\n');

export const BRIEFING = {
  id: 'brf_1',
  tenantId: 'default',
  userId: 'usr_admin',
  kind: 'Edition',
  editionId: 'edn_1',
  title: 'Morning News for November 19, 2026',
  markdown: MARKDOWN,
  status: 'Ready',
  markdownArtifactId: 'art_md',
  htmlArtifactId: 'art_html',
  itemCount: 2,
  wordCount: 40,
  readMinutes: 1,
  modelFallbacks: 2,
  pinned: false,
  isDryRun: false,
  composedUtc: '2026-11-19T06:30:00Z',
  createdUtc: '2026-11-19T06:29:00Z',
  lastUpdateUtc: '2026-11-19T06:30:00Z'
};

export const SECTIONS = [
  { id: 'bsc_1', briefingId: 'brf_1', sectionKey: 'top-of-the-news', heading: 'Top of the news', source: 'findings', sortOrder: 0 },
  { id: 'bsc_2', briefingId: 'brf_1', sectionKey: 'sources', heading: 'Sources', source: 'citations', sortOrder: 1 }
];

export const ITEMS = [
  { id: 'bit_1', briefingId: 'brf_1', sectionId: 'bsc_1', itemType: 'Finding', itemId: 'fnd_1', citationNumber: 1, sortOrder: 0 },
  { id: 'bit_2', briefingId: 'brf_1', sectionId: 'bsc_1', itemType: 'Prediction', itemId: 'prd_1', sortOrder: 1 }
];

export const FILES = [
  { id: 'art_md', kind: 'BriefingMarkdown', fileName: 'morning-news.md', contentType: 'text/markdown', bytes: 2048, expiresUtc: '2027-11-19T06:30:00Z', pinned: false },
  { id: 'art_html', kind: 'BriefingHtml', fileName: 'morning-news.html', contentType: 'text/html', bytes: 8192, expiresUtc: '2027-11-19T06:30:00Z', pinned: false }
];

export function detail(overrides = {}) {
  return { briefing: BRIEFING, sections: SECTIONS, items: ITEMS, files: FILES, filesExpired: false, editionName: 'Morning News', ...overrides };
}

export const EDITION = {
  id: 'edn_1',
  tenantId: 'default',
  userId: 'usr_admin',
  name: 'Morning News',
  description: 'Your daily briefing across all of your active topics.',
  audioEnabled: true,
  emailEnabled: true,
  enabled: true,
  freshResearchBeforeCompose: true,
  freshResearchLeadMinutes: 45,
  lookbackMode: 'SinceLastEdition',
  lookbackHours: 24,
  lastComposedUtc: '2026-11-19T06:30:00Z',
  createdUtc: '2026-11-18T06:30:00Z',
  lastUpdateUtc: '2026-11-19T06:30:00Z'
};

export const TOPICS = [
  { id: 'top_1', name: 'AI developer tools', status: 'Active', isOneTime: false },
  { id: 'top_2', name: 'Solar power', status: 'Active', isOneTime: false },
  { id: 'top_3', name: 'Chip supply', status: 'Paused', isOneTime: false }
];

export function editionDetail(overrides = {}) {
  return { edition: EDITION, topics: [], schedules: [], coveredTopicIds: ['top_1', 'top_2'], ...overrides };
}

export const TEMPLATE_CONTENT = [
  '---',
  'name: Morning News',
  'citations: numbered',
  '---',
  '',
  '# {{edition.name}} for {{date.long}}',
  '',
  '## Top of the news',
  '<!-- mmn:section source=findings tier=MustRead limit=5 order=relevance -->',
  'Lead with the most important development.',
  '',
  '## Sources',
  '<!-- mmn:section source=citations -->',
  ''
].join('\n');

export const TEMPLATE = {
  id: 'btp_1',
  tenantId: 'default',
  name: 'Morning News',
  description: 'The daily briefing across your topics.',
  defaultKey: 'morning-news',
  isDefault: true,
  currentVersion: 2,
  customized: true,
  createdUtc: '2026-11-18T06:30:00Z',
  lastUpdateUtc: '2026-11-19T06:30:00Z'
};

export const TEMPLATE_SECTIONS = [
  { key: 'top-of-the-news', heading: 'Top of the news', instruction: 'Lead with the most important development.', directive: { source: 'findings', tier: 'MustRead', limit: 5, order: 'relevance' }, line: 8 },
  { key: 'sources', heading: 'Sources', instruction: '', directive: { source: 'citations', limit: 5 }, line: 12 }
];

export function templateDetail(overrides = {}) {
  return { template: TEMPLATE, content: TEMPLATE_CONTENT, problems: [], sections: TEMPLATE_SECTIONS, shared: true, ...overrides };
}
