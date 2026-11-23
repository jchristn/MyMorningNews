// Connection state for the banner and the offline feedback queue (plan section 35.4). Host agnostic: the network
// module (expo-network), the server probe, and the timers are injected, so the tests drive it directly.

/** How long to wait before each new attempt to reach the server (milliseconds); the last value repeats. */
export const DEFAULT_RETRY_DELAYS_MS = Object.freeze([5000, 10000, 30000, 60000]);

/**
 * Tracks two facts: whether the device has a network connection (from the network module) and whether the server
 * answered the last request (from `reportSuccess()` and `reportFailure()`, which the API client calls for every
 * exchange). `status()` is `offline` (no connection), `unreachable` (a connection, but the server does not answer),
 * or `online`. While the server is unreachable the monitor probes it with a growing delay, and it probes at once when
 * the device reconnects, so the state clears itself.
 *
 * Only `isConnected === false` counts as offline. `isInternetReachable` is ignored on purpose: a self-hosted server
 * on a local network works without internet access.
 */
export class ConnectivityMonitor {
  constructor({ network, probe, setTimer, clearTimer, retryDelaysMs } = {}) {
    this.network = network || null;
    this.probe = probe || (async () => undefined);
    this.setTimer = setTimer || ((fn, ms) => setTimeout(fn, ms));
    this.clearTimer = clearTimer || ((id) => clearTimeout(id));
    this.retryDelaysMs = Array.isArray(retryDelaysMs) && retryDelaysMs.length > 0 ? retryDelaysMs : DEFAULT_RETRY_DELAYS_MS;
    this.connected = true;
    this.reachable = true;
    this.listeners = new Set();
    this.timer = null;
    this.attempt = 0;
    this.subscription = null;
    this.started = false;
  }

  /** Replace the server probe (the API client changes when the server does). */
  setProbe(probe) {
    this.probe = probe || (async () => undefined);
  }

  status() {
    if (!this.connected) return 'offline';
    return this.reachable ? 'online' : 'unreachable';
  }

  /** Call `listener(status)` whenever the status changes. Returns a function that removes the listener. */
  subscribe(listener) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  start() {
    if (this.started) return;
    this.started = true;
    if (!this.network) return;
    try {
      this.subscription = this.network.addNetworkStateListener((state) => this.applyNetworkState(state));
      void Promise.resolve(this.network.getNetworkStateAsync())
        .then((state) => {
          if (this.started) this.applyNetworkState(state);
        })
        .catch(() => undefined);
    } catch {
      // Without the native module the monitor still follows the API client's reports.
    }
  }

  stop() {
    this.started = false;
    this.cancelTimer();
    if (this.subscription && typeof this.subscription.remove === 'function') this.subscription.remove();
    this.subscription = null;
  }

  applyNetworkState(state) {
    const connected = !(state && state.isConnected === false);
    if (connected === this.connected) return;
    this.update(() => {
      this.connected = connected;
    });
    if (connected) {
      // Back on a network: find out at once whether the server answers.
      this.attempt = 0;
      void this.check();
    } else {
      this.cancelTimer();
    }
  }

  /** The server answered (any HTTP status). */
  reportSuccess() {
    this.cancelTimer();
    this.attempt = 0;
    if (this.reachable) return;
    this.update(() => {
      this.reachable = true;
    });
  }

  /** A request got no answer (offline, DNS, TLS, timeout). */
  reportFailure() {
    if (this.reachable) {
      this.update(() => {
        this.reachable = false;
      });
    }
    this.schedule();
  }

  /** Probe the server now. The probe's own exchange reports the result; a probe that throws counts as a failure. */
  async check() {
    this.cancelTimer();
    if (!this.connected) return this.status();
    try {
      await this.probe();
      this.reportSuccess();
    } catch {
      this.reportFailure();
    }
    return this.status();
  }

  schedule() {
    if (!this.started || !this.connected || this.timer !== null) return;
    const delay = this.retryDelaysMs[Math.min(this.attempt, this.retryDelaysMs.length - 1)];
    this.attempt += 1;
    this.timer = this.setTimer(() => {
      this.timer = null;
      void this.check();
    }, delay);
  }

  cancelTimer() {
    if (this.timer === null) return;
    this.clearTimer(this.timer);
    this.timer = null;
  }

  update(change) {
    const before = this.status();
    change();
    const after = this.status();
    if (before === after) return;
    for (const listener of [...this.listeners]) {
      try {
        listener(after);
      } catch {
        // A listener's failure never changes the connection state.
      }
    }
  }
}
