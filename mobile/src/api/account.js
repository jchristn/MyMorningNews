import { routes } from './routes';

/** Profile, password, usage (existing routes), and account deletion and export (contract E, Phase 14). */
export const accountApi = {
  getMe: (client) => client.get(routes.me),
  /** `{ firstName, lastName, timeZone, locale }`; email is read-only. */
  updateMe: (client, body) => client.put(routes.me, body),
  changePassword: (client, currentPassword, newPassword) => client.put(routes.mePassword, { currentPassword, newPassword }),
  /** The caller's own usage report: `{ fromUtc, toUtc, groupBy, totals, rows }`. */
  usage: (client, { from, to, groupBy = 'Task' } = {}) => client.get(routes.usageMe, { from, to, groupBy }),
  deleteAccount: (client, body) => client.post(routes.accountDelete, body || {}),
  exportAccount: (client) => client.get(routes.accountExport),
};

/** Server information: version from `GET /server-info` (existing) and client config (contract D, no auth). */
export const serverApi = {
  info: (client) => client.get(routes.serverInfo),
  clientConfig: (client) => client.request('GET', routes.clientConfig, { auth: false }),
  health: (client) => client.request('GET', routes.health, { auth: false }),
};

/** Push devices (contract C). */
export const devicesApi = {
  register: (client, { expoPushToken, platform, name, locale }) => client.post(routes.devices, { expoPushToken, platform, name, locale }),
  list: (client) => client.get(routes.devices),
  remove: (client, id) => client.delete(routes.device(id)),
  test: (client, id) => client.post(routes.deviceTest(id), {}),
};
