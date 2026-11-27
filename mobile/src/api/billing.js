import { routes } from './routes';

/** Account-bound billing. Native purchase verification is authoritative on the server. */
export const billingApi = {
  status: (client) => client.get(routes.accountBilling),
  verify: (client, body) => client.post(routes.accountPurchase, body),
  checkout: (client, offerKey) => client.post(routes.accountCheckout, { offerKey }),
};

/** Private provider secrets are write-only; responses contain only connection metadata. */
export const providerApi = {
  list: (client) => client.get(routes.accountProviders),
  save: (client, body) => client.put(routes.accountProviders, body),
  test: (client, id) => client.post(routes.accountProviderTest(id), {}),
  remove: (client, id) => client.delete(routes.accountProvider(id)),
  funding: (client, mode) => client.put(routes.accountFunding, { mode }),
};
