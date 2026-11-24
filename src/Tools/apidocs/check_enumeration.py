#!/usr/bin/env python3
"""Enforce the collection retrieval contract across every registered REST route.

The sole bare-array exception is Google's Digital Asset Links protocol file.
(c) 2026 Lattice AI LLC
"""
import json
import pathlib
import re
import routescan

ROOT = pathlib.Path(__file__).resolve().parents[3]


def main():
    routes = routescan.scan()
    supplement = json.loads((pathlib.Path(__file__).parent / 'supplement.json').read_text())['routes']
    errors = []
    count = 0
    for key, route in routes.items():
        if route.get('method') != 'GET' or route.get('path') == '/.well-known/assetlinks.json':
            continue
        response = supplement.get(key, {}).get('response')
        if response is None:
            errors.append(key + ': missing response contract')
            continue
        if isinstance(response, str) and (re.match(r'(List|IEnumerable|IReadOnlyList|ICollection)<', response) or response.endswith('[]') or response == 'array'):
            errors.append(key + ': bare collection response ' + response)
        enumerated = isinstance(response, str) and response.startswith('EnumerationResult<')
        if route.get('paged') != enumerated:
            errors.append(key + ': Paged metadata and EnumerationResult contract must agree')
        if enumerated:
            count += 1
            twin = 'POST ' + route['path'] + '/enumerate'
            other = routes.get(twin)
            if not other or other['handler'] != route['handler'] or not other.get('paged'):
                errors.append(key + ': missing equivalent POST /enumerate')
            if supplement.get(twin, {}).get('response') != response:
                errors.append(twin + ': response contract differs from GET')
    for file in (ROOT / 'sdk/csharp/MyMorningNews.Sdk/Resources').glob('*.cs'):
        text = file.read_text()
        for match in re.finditer(r'public\s+(?:async\s+)?Task<List<[^>]+>>\s+(\w+)\([^)]*\)\s*\{([^}]+)', text):
            if 'GetAsync' in match[2]:
                errors.append(str(file.relative_to(ROOT)) + ': ' + match[1] + ' still returns a bare GET list')
    if errors:
        raise SystemExit('\n'.join(errors))
    print(f'{count} collection GETs and POST twins use EnumerationQuery/EnumerationResult; protocol exception preserved.')


if __name__ == '__main__':
    main()
