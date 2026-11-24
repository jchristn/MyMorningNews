/** Traverse a collection for a composite view, using bounded EnumerationQuery requests. */
export async function enumerateObjects(load, query = {}) {
  const objects = [];
  for (let pageNumber = 1; ; pageNumber += 1) {
    const page = await load({ ...query, pageNumber, pageSize: query.pageSize || 100 });
    if (!page || !Array.isArray(page.objects) || !Number.isInteger(page.totalPages)) throw new TypeError('Expected EnumerationResult');
    objects.push(...page.objects);
    if (pageNumber >= page.totalPages || page.objects.length === 0) return objects;
  }
}
