/** Read every page for a selector or small composite view. Each HTTP retrieval remains bounded. */
export async function enumerateObjects(load) {
  const objects = [];
  for (let pageNumber = 1; ; pageNumber += 1) {
    const page = await load({ pageNumber, pageSize: 100 });
    if (!page || !Array.isArray(page.objects) || !Number.isInteger(page.totalPages)) throw new TypeError('Expected EnumerationResult');
    objects.push(...page.objects);
    if (pageNumber >= page.totalPages || page.objects.length === 0) return objects;
  }
}
