import { useState } from 'react';
import PropTypes from 'prop-types';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';

function Leaf({ value }) {
  if (value === null) return <span className="json-null">null</span>;
  switch (typeof value) {
    case 'string':
      return <span className="json-string">&quot;{value}&quot;</span>;
    case 'number':
      return <span className="json-number">{String(value)}</span>;
    case 'boolean':
      return <span className="json-boolean">{String(value)}</span>;
    default:
      return <span>{String(value)}</span>;
  }
}

Leaf.propTypes = { value: PropTypes.any };

function Node({ name, value, depth }) {
  const { t } = useTranslation();
  const isObject = value !== null && typeof value === 'object';
  const [open, setOpen] = useState(depth < 2);
  if (!isObject) {
    return (
      <div className="json-row">
        {name !== null ? <span className="json-key">{name}: </span> : null}
        <Leaf value={value} />
      </div>
    );
  }
  const entries = Array.isArray(value) ? value.map((v, i) => [String(i), v]) : Object.entries(value);
  const label = Array.isArray(value) ? t('json.items', { count: entries.length }) : t('json.keys', { count: entries.length });
  return (
    <div className="json-node">
      <button type="button" className="json-toggle" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        {open ? <ChevronDown size={12} aria-hidden="true" /> : <ChevronRight size={12} aria-hidden="true" />}
        {name !== null ? <span className="json-key">{name}: </span> : null}
        <span className="muted">{Array.isArray(value) ? '[ ]' : '{ }'} {label}</span>
      </button>
      {open ? (
        <div className="json-children">
          {entries.map(([k, v]) => (
            <Node key={k} name={k} value={v} depth={depth + 1} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

Node.propTypes = { name: PropTypes.string, value: PropTypes.any, depth: PropTypes.number };

export default function JsonTree({ value }) {
  return (
    <div className="json-tree">
      <Node name={null} value={value} depth={0} />
    </div>
  );
}

JsonTree.propTypes = { value: PropTypes.any };
