// Metro configuration. The app shares modules with the web dashboard instead of copying them (sharing.config.js):
// `@dashboard/*` resolves into dashboard/src/shared, which is watched once it exists. Bare imports made from shared
// files resolve as if they were made from this project, so the dashboard's own node_modules never produce a second
// copy of anything. `@/` resolves to src/.
const path = require('path');
const { getDefaultConfig } = require('expo/metro-config');
const { sharedRoots, sharedAvailable } = require('./sharing.config');

const projectRoot = __dirname;
const config = getDefaultConfig(projectRoot);

if (sharedAvailable) config.watchFolders = [...(config.watchFolders || []), sharedRoots.dashboardShared];

const aliases = {
  '@/': path.join(projectRoot, 'src') + path.sep,
  '@dashboard/': sharedRoots.dashboardShared + path.sep,
};
const projectOrigin = path.join(projectRoot, 'package.json');

const upstreamResolve = config.resolver.resolveRequest;
const resolve = (context, moduleName, platform) =>
  upstreamResolve ? upstreamResolve(context, moduleName, platform) : context.resolveRequest(context, moduleName, platform);

// The developer player's test tone (assets/audio/) never ships: release bundles resolve it to an empty module, and
// src/player/testTone.js only reaches it behind __DEV__.
const releaseBuild = process.env.NODE_ENV === 'production';

config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (releaseBuild && /assets\/audio\/test-tone\.[a-z0-9]+$/.test(moduleName)) return { type: 'empty' };
  for (const [prefix, target] of Object.entries(aliases)) {
    if (moduleName.startsWith(prefix)) {
      return context.resolveRequest(context, target + moduleName.slice(prefix.length), platform);
    }
  }
  const fromShared = context.originModulePath.startsWith(sharedRoots.dashboardShared + path.sep);
  if (fromShared && !moduleName.startsWith('.') && !path.isAbsolute(moduleName)) {
    return resolve({ ...context, originModulePath: projectOrigin }, moduleName, platform);
  }
  return resolve(context, moduleName, platform);
};

module.exports = config;
