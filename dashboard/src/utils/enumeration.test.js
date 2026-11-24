import { describe, expect, it, vi } from 'vitest';
import { enumerateObjects } from './enumeration.js';
import { enumerationResult } from '../test/enumeration.js';

describe('enumeration traversal', () => {
  it('reads every bounded page without dropping records after the first page', async () => {
    const rows = Array.from({ length: 205 }, (_, id) => ({ id }));
    const load = vi.fn(async (query) => enumerationResult(rows, query));
    expect(await enumerateObjects(load)).toEqual(rows);
    expect(load.mock.calls.map(([q]) => q)).toEqual([1, 2, 3].map((pageNumber) => ({ pageNumber, pageSize: 100 })));
  });
  it('handles empty collections and rejects the old bare-array contract', async () => {
    expect(await enumerateObjects(async () => enumerationResult([]))).toEqual([]);
    await expect(enumerateObjects(async () => [])).rejects.toThrow('EnumerationResult');
  });
});
