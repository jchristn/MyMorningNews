// Jest resolver: mirrors metro.config.js. Bare imports made from shared dashboard modules resolve as if they were made
// from this project, so tests never depend on the dashboard's node_modules and never load a second copy.
const path = require('path');
const { sharedRoots } = require('./sharing.config');

const projectRoot = __dirname;
const sharedPrefix = sharedRoots.dashboardShared + path.sep;

function isBare(request) {
  return !request.startsWith('.') && !path.isAbsolute(request);
}

module.exports = function resolveRequest(request, options) {
  const fromShared = typeof options.basedir === 'string' && (options.basedir + path.sep).startsWith(sharedPrefix);
  if (fromShared && isBare(request)) {
    return options.defaultResolver(request, { ...options, basedir: projectRoot });
  }
  return options.defaultResolver(request, options);
};
