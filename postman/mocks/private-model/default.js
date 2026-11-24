// (c) 2026 Lattice AI LLC
// Minimal OpenAI-compatible dependency for ownership and no-fallback tests. Accepts only a synthetic key.
const http = require('http');
http.createServer(async (req, res) => {
  const reply = (status, data) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
  const path = new URL(req.url, 'http://127.0.0.1').pathname;
  const state = await pm.state.get('private-model.requests') || { accepted: 0, rejected: 0 };
  if (path === '/__state') return reply(200, state);
  if (req.headers.authorization !== 'Bearer sk_test_private_1234') {
    state.rejected++; await pm.state.set('private-model.requests', state);
    return reply(401, { error: { message: 'Synthetic private key required', type: 'authentication_error' } });
  }
  const input = typeof req.body === 'string' ? JSON.parse(req.body) : req.body || {};
  if (path.endsWith('/audio/speech') && req.method === 'POST') {
    state.accepted++; state.speechInput = input.input; state.speechVoice = input.voice;
    await pm.state.set('private-model.requests', state);
    const wav = Buffer.alloc(44 + 1600);
    wav.write('RIFF', 0); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
    wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
    wav.writeUInt32LE(8000, 24); wav.writeUInt32LE(16000, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
    wav.write('data', 36); wav.writeUInt32LE(1600, 40);
    res.writeHead(200, { 'Content-Type': 'audio/wav' }); return res.end(wav);
  }
  if (path.endsWith('/chat/completions') && req.method === 'POST') {
    state.accepted++; await pm.state.set('private-model.requests', state);
    if (input.stream) {
      res.writeHead(200, { 'Content-Type': 'text/event-stream' });
      res.write('data: ' + JSON.stringify({ id: 'chatcmpl_private', object: 'chat.completion.chunk', model: input.model, choices: [{ index: 0, delta: { role: 'assistant', content: 'ready' }, finish_reason: null }] }) + '\n\n');
      res.write('data: ' + JSON.stringify({ id: 'chatcmpl_private', object: 'chat.completion.chunk', model: input.model, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }], usage: { prompt_tokens: 5, completion_tokens: 1, total_tokens: 6 } }) + '\n\n');
      return res.end('data: [DONE]\n\n');
    }
    return reply(200, { id: 'chatcmpl_private', object: 'chat.completion', model: input.model, choices: [{ index: 0, message: { role: 'assistant', content: 'ready' }, finish_reason: 'stop' }], usage: { prompt_tokens: 5, completion_tokens: 1, total_tokens: 6 } });
  }
  if (path.endsWith('/embeddings') && req.method === 'POST') {
    state.accepted++; await pm.state.set('private-model.requests', state);
    const inputs = Array.isArray(input.input) ? input.input : [input.input];
    return reply(200, { object: 'list', model: input.model, data: inputs.map((_, index) => ({ object: 'embedding', index, embedding: [0.1, 0.2, 0.3] })), usage: { prompt_tokens: 2, total_tokens: 2 } });
  }
  return reply(501, { error: { type: 'simulation_not_implemented', path } });
}).listen(process.env.PORT || 12194);
