import { enumerateObjects } from '@/api/enumeration';

test('composite views traverse all bounded pages, preserving query filters', async () => {
  const rows = Array.from({ length: 205 }, (_, id) => ({ id }));
  const load = jest.fn(async ({ pageNumber, pageSize }) => ({ success: true, objects: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize), totalPages: Math.ceil(rows.length / pageSize), pageNumber, pageSize, totalRecords: rows.length, totalMs: 0 }));
  expect(await enumerateObjects(load, { disposition: 'Fetched' })).toEqual(rows);
  expect(load.mock.calls.map(([q]) => q)).toEqual([1, 2, 3].map((pageNumber) => ({ disposition: 'Fetched', pageNumber, pageSize: 100 })));
});

test('bare-array retrieval is refused', async () => {
  await expect(enumerateObjects(async () => [])).rejects.toThrow('EnumerationResult');
});
