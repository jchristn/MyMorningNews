/** A real page fixture, including totals and empty pages past the end. */
export function enumerationResult(objects, query = {}) {
  const pageNumber = Math.max(1, query?.pageNumber || 1);
  const pageSize = Math.min(1000, Math.max(1, query?.pageSize || 10));
  return { success: true, objects: objects.slice((pageNumber - 1) * pageSize, pageNumber * pageSize), pageNumber, pageSize, totalRecords: objects.length, totalPages: Math.ceil(objects.length / pageSize), totalMs: 0 };
}
