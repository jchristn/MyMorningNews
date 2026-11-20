import { useMemo } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import MarkdownView from '../../components/MarkdownView.jsx';
import { citationAnchor, linkCitations, matchSections, sectionAnchor, splitBriefingMarkdown, splitSourceList } from '../../utils/briefing.js';

const CITE_PREFIX = '#briefing-cite-';

export function scrollToAnchor(id) {
  const el = typeof document !== 'undefined' ? document.getElementById(id) : null;
  if (!el) return;
  if (typeof el.scrollIntoView === 'function') el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  el.classList.add('anchor-flash');
  window.setTimeout(() => el.classList.remove('anchor-flash'), 1600);
}

// In-page citation links scroll to the Sources entry; every other link opens in a new tab.
function BriefingLink({ node: _node, href, children, ...props }) {
  const { t } = useTranslation();
  if (href && href.startsWith(CITE_PREFIX)) {
    const number = href.slice(CITE_PREFIX.length);
    return (
      <a
        href={href}
        className="citation-link"
        aria-label={t('briefings.reader.citation', { number })}
        onClick={(e) => {
          e.preventDefault();
          scrollToAnchor(citationAnchor(number));
        }}
      >
        {children}
      </a>
    );
  }
  return (
    <a {...props} href={href} target="_blank" rel="noreferrer">
      {children}
    </a>
  );
}

BriefingLink.propTypes = { node: PropTypes.object, href: PropTypes.string, children: PropTypes.node };

const COMPONENTS = { a: BriefingLink };

function isSources(chunk) {
  return (chunk.section && chunk.section.source === 'citations') || /^sources$/i.test(chunk.heading);
}

// A composed briefing, rendered sanitized: the opening, then each section under an anchor the outline links to.
// Numbered citations link to the Sources list, whose entries carry their own anchors. sectionExtra(chunk) renders
// extra content under a section (the inline feedback slot). showTitle renders the level-one title. currentSectionId
// highlights the section the audio player is reading.
export default function BriefingMarkdown({ markdown, sections = [], showTitle = false, sectionExtra = null, currentSectionId = null }) {
  const parsed = useMemo(() => splitBriefingMarkdown(markdown), [markdown]);
  const chunks = useMemo(() => matchSections(parsed.chunks, sections), [parsed, sections]);
  return (
    <article className="briefing-article">
      {showTitle && parsed.title ? <h1 className="briefing-title">{parsed.title}</h1> : null}
      {parsed.preamble ? <MarkdownView source={linkCitations(parsed.preamble)} components={COMPONENTS} className="briefing-opening" /> : null}
      {chunks.map((chunk) => {
        const list = isSources(chunk) ? splitSourceList(chunk.body) : { entries: [], trailing: '' };
        const sources = list.entries;
        const speaking = Boolean(currentSectionId && chunk.section && chunk.section.id === currentSectionId);
        return (
          <section
            key={chunk.key}
            id={sectionAnchor(chunk.key)}
            className={'briefing-section' + (speaking ? ' speaking' : '')}
            aria-label={chunk.heading}
            aria-current={speaking ? 'true' : undefined}
            data-speaking={speaking ? 'true' : undefined}
            tabIndex={-1}
          >
            <h2>{chunk.heading}</h2>
            {sources.length > 0 ? (
              <ol className="briefing-sources markdown-view">
                {sources.map((s) => (
                  <li key={s.number} id={citationAnchor(s.number)} value={s.number} tabIndex={-1}>
                    <MarkdownView source={s.text} className="inline" />
                  </li>
                ))}
              </ol>
            ) : null}
            {sources.length > 0 && list.trailing ? <MarkdownView source={list.trailing} components={COMPONENTS} className="briefing-sources-note" /> : null}
            {sources.length > 0 ? null : (
              <MarkdownView source={linkCitations(chunk.body)} components={COMPONENTS} />
            )}
            {sectionExtra ? sectionExtra(chunk) : null}
          </section>
        );
      })}
    </article>
  );
}

BriefingMarkdown.propTypes = {
  markdown: PropTypes.string,
  sections: PropTypes.array,
  showTitle: PropTypes.bool,
  sectionExtra: PropTypes.func,
  currentSectionId: PropTypes.string
};
