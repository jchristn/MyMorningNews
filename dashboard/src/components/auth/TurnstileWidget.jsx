import { useEffect, useRef } from 'react';
import PropTypes from 'prop-types';

const SCRIPT_URL = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
let loading = null;

function loadScript() {
  if (typeof window === 'undefined') return Promise.reject(new Error('No window'));
  if (window.turnstile) return Promise.resolve(window.turnstile);
  if (!loading) {
    loading = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = SCRIPT_URL;
      script.async = true;
      script.onload = () => resolve(window.turnstile);
      script.onerror = () => {
        loading = null;
        reject(new Error('Turnstile did not load'));
      };
      document.head.appendChild(script);
    });
  }
  return loading;
}

// The Cloudflare Turnstile human check, shown on sign-up only when the server has a site key. The script loads only
// then, so self-hosted installs never contact Cloudflare.
export default function TurnstileWidget({ siteKey, onToken }) {
  const host = useRef(null);
  const callback = useRef(onToken);
  callback.current = onToken;

  useEffect(() => {
    let widgetId = null;
    let live = true;
    loadScript()
      .then((turnstile) => {
        if (!live || !host.current || !turnstile) return;
        widgetId = turnstile.render(host.current, {
          sitekey: siteKey,
          callback: (token) => callback.current(token),
          'expired-callback': () => callback.current(''),
          'error-callback': () => callback.current('')
        });
      })
      .catch(() => callback.current(''));
    return () => {
      live = false;
      if (widgetId !== null && window.turnstile) window.turnstile.remove(widgetId);
    };
  }, [siteKey]);

  return <div ref={host} className="turnstile" />;
}

TurnstileWidget.propTypes = { siteKey: PropTypes.string.isRequired, onToken: PropTypes.func.isRequired };
