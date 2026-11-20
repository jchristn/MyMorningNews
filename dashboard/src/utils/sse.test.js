import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './api.js';
import { createSseParser } from './sse.js';
import { mergeStage, retryAction, withAllStages } from '../views/runs/runParts.js';

function collect() {
  const events = [];
  return { events, parser: createSseParser((e) => events.push(e)) };
}

describe('createSseParser', () => {
  it('parses events across arbitrary chunks, with JSON data, ids, and comments', () => {
    const { events, parser } = collect();
    parser.push('id: 1\nevent: run\nda');
    parser.push('ta: {"id":"run_1","status":"Running"}\n');
    parser.push('\n: keep-alive comment\n\nevent: ping\ndata: {}\n\n');
    expect(events).toEqual([
      { event: 'run', data: { id: 'run_1', status: 'Running' }, raw: '{"id":"run_1","status":"Running"}', id: '1' },
      { event: 'ping', data: {}, raw: '{}', id: '1' }
    ]);
  });

  it('joins multi-line data, keeps non-JSON text, and handles CRLF split between chunks', () => {
    const { events, parser } = collect();
    parser.push('event: note\r');
    parser.push('\ndata: first\r\ndata: second\r');
    parser.push('\n\r\n');
    expect(events).toEqual([{ event: 'note', data: 'first\nsecond', raw: 'first\nsecond', id: null }]);
  });

  it('defaults the event name to message and flushes a final event without a blank line', () => {
    const { events, parser } = collect();
    parser.push('data: 42');
    expect(events).toHaveLength(0);
    parser.flush();
    expect(events).toEqual([{ event: 'message', data: 42, raw: '42', id: null }]);
  });
});

function streamResponse(chunks, status = 200) {
  const encoder = new TextEncoder();
  let index = 0;
  const reader = { read: vi.fn(async () => (index < chunks.length ? { value: encoder.encode(chunks[index++]), done: false } : { value: undefined, done: true })), cancel: vi.fn(async () => undefined) };
  return { response: { status, ok: status >= 200 && status < 300, body: { getReader: () => reader }, text: async () => '' }, reader };
}

describe('ApiClient.streamRunEvents', () => {
  it('sends the bearer token, emits each event, and stops at done', async () => {
    const { response, reader } = streamResponse(['event: stage\ndata: {"stage":"Search","status":"Running"}\n\n', 'event: done\ndata: {"id":"run_1","status":"Completed"}\n\n', 'event: never\ndata: {}\n\n']);
    const fetch = vi.fn(async () => response);
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    const onEvent = vi.fn();
    const controller = new AbortController();
    await expect(api.streamRunEvents('run 1', { onEvent, signal: controller.signal })).resolves.toBe('done');
    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:12100/v1.0/api/runs/run%201/events');
    expect(init.headers.Authorization).toBe('Bearer tok');
    expect(init.headers.Accept).toBe('text/event-stream');
    expect(init.signal).toBe(controller.signal);
    expect(onEvent.mock.calls.map((c) => c[0].event)).toEqual(['stage', 'done']);
    expect(onEvent.mock.calls[1][0].data).toEqual({ id: 'run_1', status: 'Completed' });
    expect(reader.cancel).toHaveBeenCalled();
  });

  it('resolves timeout so the caller reconnects, and null when the stream closes early', async () => {
    const timeout = streamResponse(['event: ping\ndata: {}\n\nevent: timeout\ndata: {"reconnect":true}\n\n']);
    const closed = streamResponse(['event: run\ndata: {"id":"run_1"}\n\n']);
    const fetch = vi.fn().mockResolvedValueOnce(timeout.response).mockResolvedValueOnce(closed.response);
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await expect(api.streamRunEvents('run_1')).resolves.toBe('timeout');
    await expect(api.streamRunEvents('run_1')).resolves.toBe('run');
  });

  it('rejects with ApiError for an error status or when the response cannot stream', async () => {
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch: vi.fn(async () => ({ status: 404, ok: false, text: async () => '{"error":"NotFound","message":"Run was not found."}' })) });
    await expect(api.streamRunEvents('run_x')).rejects.toMatchObject({ status: 404, code: 'NotFound' });
    const noBody = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch: vi.fn(async () => ({ status: 200, ok: true, body: null })) });
    await expect(noBody.streamRunEvents('run_1')).rejects.toBeInstanceOf(ApiError);
  });

  it('retries a run from a stage or from the start, and lists sources', async () => {
    const fetch = vi.fn(async () => ({ status: 202, ok: true, text: async () => '{"id":"run_1"}' }));
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await api.retryRun('run_1', 'Predict');
    await api.retryRun('run_1');
    await api.listSources({ topicId: 'top_1', runId: 'run_1', search: 'evals', pageNumber: 1, pageSize: 25 });
    expect(fetch.mock.calls[0][0]).toBe('http://127.0.0.1:12100/v1.0/api/runs/run_1/retry?fromStage=Predict');
    expect(fetch.mock.calls[0][1].method).toBe('POST');
    expect(fetch.mock.calls[1][0]).toBe('http://127.0.0.1:12100/v1.0/api/runs/run_1/retry');
    expect(fetch.mock.calls[2][0]).toBe('http://127.0.0.1:12100/v1.0/api/sources?topicId=top_1&runId=run_1&search=evals&pageNumber=1&pageSize=25');
  });
});

describe('run stage helpers', () => {
  it('lists all fifteen stages in order with Pending placeholders', () => {
    const all = withAllStages([{ stage: 'Search', status: 'Running', sortOrder: 7 }, { stage: 'Custom', status: 'Succeeded' }]);
    expect(all).toHaveLength(16);
    expect(all.map((s) => s.stage).slice(0, 3)).toEqual(['AssembleContext', 'PlanQueries', 'Search']);
    expect(all[2]).toMatchObject({ stage: 'Search', status: 'Running', sortOrder: 2 });
    expect(all[0]).toMatchObject({ status: 'Pending', sortOrder: 0 });
    expect(all[14].stage).toBe('Notify');
    expect(all[15]).toMatchObject({ stage: 'Custom', sortOrder: 15 });
  });

  it('merges a stage event by name', () => {
    const stages = [{ stage: 'Search', status: 'Running', itemsIn: 0 }];
    expect(mergeStage(stages, { stage: 'Search', status: 'Succeeded', itemsOut: 9 })).toEqual([{ stage: 'Search', status: 'Succeeded', itemsIn: 0, itemsOut: 9 }]);
    expect(mergeStage(stages, { stage: 'Fetch', status: 'Running' })).toHaveLength(2);
    expect(mergeStage(null, null)).toEqual([]);
  });

  it('offers a resume from Synthesize onward, Run again for failed earlier stages, and nothing while active', () => {
    const done = { status: 'PartiallySucceeded' };
    expect(retryAction(done, { stage: 'Predict', status: 'Failed' })).toEqual({ kind: 'resume', fromStage: 'Predict' });
    expect(retryAction(done, { stage: 'Synthesize', status: 'Succeeded' })).toEqual({ kind: 'resume', fromStage: 'Synthesize' });
    expect(retryAction(done, { stage: 'Notify', status: 'Skipped' })).toEqual({ kind: 'resume', fromStage: 'Notify' });
    expect(retryAction(done, { stage: 'Fetch', status: 'Failed' })).toEqual({ kind: 'new', fromStage: null });
    expect(retryAction(done, { stage: 'Fetch', status: 'Succeeded' })).toBeNull();
    expect(retryAction(done, { stage: 'Predict', status: 'Pending' })).toBeNull();
    expect(retryAction({ status: 'Running' }, { stage: 'Predict', status: 'Failed' })).toBeNull();
  });
});
