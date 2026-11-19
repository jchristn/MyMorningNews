import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useAuth } from '../context/AuthContext.jsx';
import { buildPath, flattenOpenApiSpec, getParameterDefault, getRequestBodyTemplate, pathParameterNames } from '../utils/openApi.js';
import useLocalStorage from './useLocalStorage.js';

export const HISTORY_KEY = 'api_explorer_history';
export const HISTORY_LIMIT = 12;

function emptyRow() {
  return { key: '', value: '' };
}

// State and actions for the API Explorer. Authentication is inherited from the dashboard session.
export default function useApiExplorer() {
  const { apiClient, identity } = useAuth();
  const [spec, setSpec] = useState(null);
  const [specError, setSpecError] = useState(null);
  const [operationId, setOperationId] = useLocalStorage('mymorningnews.explorer.operation', null);
  const [pathParams, setPathParams] = useState({});
  const [queryParams, setQueryParams] = useState([emptyRow()]);
  const [headers, setHeaders] = useState([emptyRow()]);
  const [body, setBody] = useState('');
  const [response, setResponse] = useState(null);
  const [running, setRunning] = useState(false);
  const [history, setHistory] = useLocalStorage(HISTORY_KEY, []);
  const abortRef = useRef(null);

  const loadSpec = useCallback(async () => {
    setSpecError(null);
    try {
      setSpec(await apiClient.getOpenApiSpec());
    } catch (err) {
      setSpecError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    loadSpec();
  }, [loadSpec]);

  const operations = useMemo(() => flattenOpenApiSpec(spec), [spec]);
  const operation = operations.find((o) => o.id === operationId) || null;

  const selectOperation = useCallback(
    (op) => {
      setOperationId(op.id);
      const defaults = {};
      for (const name of pathParameterNames(op.path)) {
        const param = op.parameters.find((p) => p.in === 'path' && p.name === name);
        defaults[name] = name === 'tenantId' && identity ? identity.tenantId : getParameterDefault(param);
      }
      setPathParams(defaults);
      const query = op.parameters.filter((p) => p.in === 'query').map((p) => ({ key: p.name, value: getParameterDefault(p) }));
      setQueryParams(query.length > 0 ? query : [emptyRow()]);
      setHeaders([emptyRow()]);
      setBody(getRequestBodyTemplate(op.requestBody, spec ? spec.components : null) || (op.method === 'POST' || op.method === 'PUT' ? '{\n}' : ''));
      setResponse(null);
    },
    [identity, setOperationId, spec]
  );

  const resolvedPath = operation ? buildPath(operation.path, pathParams) : '';
  const queryObject = Object.fromEntries(queryParams.filter((r) => r.key).map((r) => [r.key, r.value]));
  const headerObject = Object.fromEntries(headers.filter((r) => r.key).map((r) => [r.key, r.value]));
  const search = new URLSearchParams(Object.entries(queryObject).filter(([, v]) => v !== '')).toString();
  const resolvedUrl = operation ? apiClient.baseUrl + resolvedPath + (search ? '?' + search : '') : '';

  let bodyError = null;
  if (body && body.trim()) {
    try {
      JSON.parse(body);
    } catch (err) {
      bodyError = err.message;
    }
  }

  const execute = useCallback(async () => {
    if (!operation) return;
    const controller = new AbortController();
    abortRef.current = controller;
    setRunning(true);
    const started = performance.now();
    try {
      const res = await apiClient.executeExplorer({
        method: operation.method,
        path: resolvedPath,
        query: queryObject,
        headers: headerObject,
        body: body && body.trim() ? body : null,
        signal: controller.signal
      });
      const responseHeaders = {};
      res.headers.forEach((v, k) => {
        responseHeaders[k] = v;
      });
      let text = '';
      const type = res.headers.get('content-type') || '';
      if (res.body && (type.includes('text/event-stream') || res.headers.get('transfer-encoding') === 'chunked')) {
        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          text += decoder.decode(value, { stream: true });
          setResponse({ status: res.status, statusText: res.statusText, headers: responseHeaders, body: text, durationMs: performance.now() - started, streaming: true });
        }
      } else {
        text = await res.text();
      }
      const result = { status: res.status, statusText: res.statusText, headers: responseHeaders, body: text, durationMs: performance.now() - started, bytes: new Blob([text]).size };
      setResponse(result);
      setHistory((list) =>
        [{ id: Date.now(), operationId: operation.id, method: operation.method, path: resolvedPath, pathParams, queryParams, headers, body, status: res.status, at: new Date().toISOString() }, ...list].slice(0, HISTORY_LIMIT)
      );
    } catch (err) {
      setResponse({ status: 0, statusText: err && err.name === 'AbortError' ? 'Canceled' : 'Error', headers: {}, body: err && err.message ? err.message : String(err), durationMs: performance.now() - started, error: true });
    } finally {
      setRunning(false);
      abortRef.current = null;
    }
    // queryObject and headerObject are derived from queryParams and headers.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient, operation, resolvedPath, queryParams, headers, body, pathParams, setHistory]);

  const cancel = useCallback(() => {
    if (abortRef.current) abortRef.current.abort();
  }, []);

  const loadHistory = useCallback(
    (entry) => {
      setOperationId(entry.operationId);
      setPathParams(entry.pathParams || {});
      setQueryParams(entry.queryParams && entry.queryParams.length > 0 ? entry.queryParams : [emptyRow()]);
      setHeaders(entry.headers && entry.headers.length > 0 ? entry.headers : [emptyRow()]);
      setBody(entry.body || '');
      setResponse(null);
    },
    [setOperationId]
  );

  const deleteHistory = useCallback((id) => setHistory((list) => list.filter((e) => e.id !== id)), [setHistory]);

  return {
    spec,
    specError,
    loadSpec,
    operations,
    operation,
    selectOperation,
    pathParams,
    setPathParams,
    queryParams,
    setQueryParams,
    headers,
    setHeaders,
    body,
    setBody,
    bodyError,
    response,
    running,
    execute,
    cancel,
    resolvedUrl,
    headerObject,
    history,
    loadHistory,
    deleteHistory,
    token: apiClient.token
  };
}
