const root = '/v1.0/api/catalog';
const publication = (id) => root + '/' + encodeURIComponent(id);
const issue = (publicationId, issueId) => publication(publicationId) + '/issues/' + encodeURIComponent(issueId);

/** Shared publications use subscriber authorization, never canonical workspace credentials. */
export const catalogApi = {
  list: (client, query) => client.get(root, query),
  subscriptions: (client, query) => client.get(root + '/subscriptions', query),
  subscribe: (client, id, preferences = {}) => client.put(publication(id) + '/subscription', preferences),
  unsubscribe: (client, id) => client.delete(publication(id) + '/subscription'),
  issues: (client, publicationId, query) => client.get(publicationId ? publication(publicationId) + '/issues' : root + '/issues', query),
  issue: (client, publicationId, issueId) => client.get(issue(publicationId, issueId)),
  progress: (client, publicationId, issueId) => client.get(issue(publicationId, issueId) + '/progress'),
  saveProgress: (client, publicationId, issueId, body) => client.put(issue(publicationId, issueId) + '/progress', body),
};
