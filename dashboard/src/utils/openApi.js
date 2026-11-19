const METHODS = ['get', 'post', 'put', 'patch', 'delete', 'head', 'options'];

// Flatten an OpenAPI document into a list of operations.
export function flattenOpenApiSpec(spec) {
  if (!spec || !spec.paths) return [];
  const ops = [];
  for (const [path, item] of Object.entries(spec.paths)) {
    const shared = Array.isArray(item.parameters) ? item.parameters : [];
    for (const method of METHODS) {
      const op = item[method];
      if (!op) continue;
      const params = [...shared, ...(Array.isArray(op.parameters) ? op.parameters : [])].map((p) => resolveRef(p, spec.components));
      ops.push({
        id: method.toUpperCase() + ' ' + path,
        operationId: op.operationId || null,
        tag: Array.isArray(op.tags) && op.tags.length > 0 ? op.tags[0] : 'Other',
        method: method.toUpperCase(),
        path,
        summary: op.summary || '',
        description: op.description || '',
        parameters: params,
        requestBody: op.requestBody ? resolveRef(op.requestBody, spec.components) : null,
        responses: op.responses || {}
      });
    }
  }
  return ops.sort((a, b) => a.tag.localeCompare(b.tag) || a.path.localeCompare(b.path) || a.method.localeCompare(b.method));
}

function resolveRef(node, components) {
  if (!node || !node.$ref || !components) return node;
  const parts = node.$ref.replace(/^#\/components\//, '').split('/');
  let current = components;
  for (const part of parts) current = current ? current[part] : undefined;
  return current || node;
}

// Default value for a parameter: example, default, first enum value, then a zero value for its type.
export function getParameterDefault(parameter) {
  if (!parameter) return '';
  if (parameter.example !== undefined) return String(parameter.example);
  const schema = parameter.schema || {};
  if (schema.example !== undefined) return String(schema.example);
  if (schema.default !== undefined) return String(schema.default);
  if (Array.isArray(schema.enum) && schema.enum.length > 0) return String(schema.enum[0]);
  return '';
}

function sample(schema, components, depth = 0) {
  if (!schema || depth > 6) return null;
  if (schema.$ref) return sample(resolveRef(schema, components), components, depth + 1);
  if (schema.example !== undefined) return schema.example;
  if (Array.isArray(schema.allOf)) return Object.assign({}, ...schema.allOf.map((s) => sample(s, components, depth + 1) || {}));
  if (Array.isArray(schema.enum) && schema.enum.length > 0) return schema.enum[0];
  switch (schema.type) {
    case 'object': {
      const out = {};
      for (const [k, v] of Object.entries(schema.properties || {})) out[k] = sample(v, components, depth + 1);
      return out;
    }
    case 'array':
      return [sample(schema.items, components, depth + 1)].filter((v) => v !== null);
    case 'integer':
    case 'number':
      return 0;
    case 'boolean':
      return false;
    case 'string':
      return '';
    default:
      return schema.properties ? sample({ ...schema, type: 'object' }, components, depth + 1) : null;
  }
}

// JSON template for a request body, resolving $ref and merging allOf.
export function getRequestBodyTemplate(requestBody, components) {
  if (!requestBody || !requestBody.content) return '';
  const media = requestBody.content['application/json'] || Object.values(requestBody.content)[0];
  if (!media) return '';
  if (media.example !== undefined) return JSON.stringify(media.example, null, 2);
  const value = sample(media.schema, components);
  return value === null ? '' : JSON.stringify(value, null, 2);
}

// Fill {name} placeholders with encoded values.
export function buildPath(path, pathParams) {
  return path.replace(/\{([^}]+)\}/g, (_, name) => encodeURIComponent(pathParams[name] ?? ''));
}

export function pathParameterNames(path) {
  return Array.from(path.matchAll(/\{([^}]+)\}/g)).map((m) => m[1]);
}

function shellQuote(s) {
  return "'" + String(s).replace(/'/g, "'\\''") + "'";
}

// Copyable code snippets for the current request: curl, fetch, and C# HttpClient.
export function buildCodeSnippets({ method, url, headers, body }) {
  const hasBody = body && method !== 'GET' && method !== 'HEAD';
  const headerEntries = Object.entries(headers || {}).filter(([, v]) => v !== undefined && v !== null && v !== '');
  const curl = ['curl -X ' + method + ' ' + shellQuote(url), ...headerEntries.map(([k, v]) => '  -H ' + shellQuote(k + ': ' + v)), ...(hasBody ? ['  -d ' + shellQuote(body)] : [])].join(' \\\n');
  const fetchHeaders = headerEntries.length > 0 ? '  headers: ' + JSON.stringify(Object.fromEntries(headerEntries), null, 2).replace(/\n/g, '\n  ') + ',\n' : '';
  const fetchSnippet =
    'const response = await fetch(' + JSON.stringify(url) + ', {\n  method: ' + JSON.stringify(method) + ',\n' + fetchHeaders + (hasBody ? '  body: ' + JSON.stringify(body) + '\n' : '') + '});\nconst data = await response.json();';
  const csLines = [
    'using (HttpClient client = new HttpClient())',
    'using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(' + JSON.stringify(method) + '), ' + JSON.stringify(url) + '))',
    '{',
    ...headerEntries.filter(([k]) => k.toLowerCase() !== 'content-type').map(([k, v]) => '    request.Headers.TryAddWithoutValidation(' + JSON.stringify(k) + ', ' + JSON.stringify(v) + ');'),
    ...(hasBody ? ['    request.Content = new StringContent(' + JSON.stringify(body) + ', Encoding.UTF8, "application/json");'] : []),
    '    using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))',
    '    {',
    '        string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);',
    '    }',
    '}'
  ];
  return { curl, fetch: fetchSnippet, csharp: csLines.join('\n') };
}
