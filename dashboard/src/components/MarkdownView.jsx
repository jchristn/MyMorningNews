import PropTypes from 'prop-types';
import ReactMarkdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import remarkGfm from 'remark-gfm';

// Sanitized GitHub-flavored Markdown. Links open in a new tab.
export default function MarkdownView({ source, className = '' }) {
  if (!source) return null;
  return (
    <div className={'markdown-view ' + className}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={{
          // eslint-disable-next-line no-unused-vars
          a: ({ node, ...props }) => <a {...props} target="_blank" rel="noreferrer" />
        }}
      >
        {source}
      </ReactMarkdown>
    </div>
  );
}

MarkdownView.propTypes = {
  source: PropTypes.string,
  className: PropTypes.string
};
