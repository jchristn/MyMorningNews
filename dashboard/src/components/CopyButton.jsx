import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Check, Copy } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export async function copyText(value) {
  const text = String(value ?? '');
  if (navigator.clipboard && window.isSecureContext !== false) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall through to the textarea fallback.
    }
  }
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  area.select();
  let ok = false;
  try {
    ok = document.execCommand('copy');
  } catch {
    ok = false;
  }
  document.body.removeChild(area);
  return ok;
}

// The one copy control: copies the exact value and shows a checkmark briefly, with no layout shift.
export default function CopyButton({ value, labelKey = 'common.copy', size = 14, className = '' }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const timer = useRef(null);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  async function onClick(event) {
    event.stopPropagation();
    const ok = await copyText(value);
    if (!ok) return;
    setCopied(true);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setCopied(false), 1500);
  }

  const label = copied ? t('common.copied') : t(labelKey);
  return (
    <button type="button" className={'icon-button copy-button ' + className} onClick={onClick} aria-label={label} title={label} data-ignore-row-click="true">
      {copied ? <Check size={size} aria-hidden="true" /> : <Copy size={size} aria-hidden="true" />}
    </button>
  );
}

CopyButton.propTypes = {
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  labelKey: PropTypes.string,
  size: PropTypes.number,
  className: PropTypes.string
};
