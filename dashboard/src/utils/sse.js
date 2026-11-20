// A minimal server-sent events parser for fetch streaming (EventSource cannot send an Authorization header).
// push() takes decoded text in any chunking; each complete event (ended by a blank line) calls onEvent with
// { event, data, raw, id }, where data is the JSON-parsed payload when it parses and raw is the text. Comment lines
// (starting with ':') are ignored. flush() dispatches a final event that was not followed by a blank line.
export function createSseParser(onEvent) {
  let buffer = '';
  let event = '';
  let data = [];
  let id = null;

  function dispatch() {
    if (data.length === 0 && !event) return;
    const raw = data.join('\n');
    let parsed = raw;
    try {
      parsed = raw ? JSON.parse(raw) : null;
    } catch {
      parsed = raw;
    }
    const out = { event: event || 'message', data: parsed, raw, id };
    event = '';
    data = [];
    onEvent(out);
  }

  function line(text) {
    if (text === '') {
      dispatch();
      return;
    }
    if (text.startsWith(':')) return;
    const colon = text.indexOf(':');
    const field = colon < 0 ? text : text.slice(0, colon);
    let value = colon < 0 ? '' : text.slice(colon + 1);
    if (value.startsWith(' ')) value = value.slice(1);
    if (field === 'event') event = value;
    else if (field === 'data') data.push(value);
    else if (field === 'id') id = value;
  }

  return {
    push(text) {
      let work = buffer + text;
      // A trailing '\r' may be the first half of '\r\n'; hold it for the next chunk.
      const hold = work.endsWith('\r') ? '\r' : '';
      if (hold) work = work.slice(0, -1);
      const parts = work.split(/\r\n|\r|\n/);
      buffer = parts.pop() + hold;
      for (const part of parts) line(part);
    },
    flush() {
      if (buffer) line(buffer);
      buffer = '';
      dispatch();
    }
  };
}
