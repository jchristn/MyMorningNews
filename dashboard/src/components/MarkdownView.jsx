import PropTypes from 'prop-types';
import ReactMarkdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import remarkGfm from 'remark-gfm';

function ExternalLink({ node: _node, ...props }) {
  return <a {...props} target="_blank" rel="noreferrer" />;
}

ExternalLink.propTypes = { node: PropTypes.object };

// Sanitized GitHub-flavored Markdown. Links open in a new tab. components overrides element renderers (for example
// in-page citation links); raw HTML in the source is never rendered.
export default function MarkdownView({ source, className = '', components = null }) {
  if (!source) return null;
  return (
    <div className={'markdown-view ' + className}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]} components={{ a: ExternalLink, ...(components || {}) }}>
        {source}
      </ReactMarkdown>
    </div>
  );
}

MarkdownView.propTypes = {
  source: PropTypes.string,
  className: PropTypes.string,
  components: PropTypes.object
};
