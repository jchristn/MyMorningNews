/**
 * Expo app configuration for the My Morning News mobile app (plan section 35).
 *
 * MAINTAINER: before the first store build
 *   - MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID: the EAS project id (`npx eas-cli@latest init`). No project exists yet, so
 *     the default below is an empty placeholder: push tokens and EAS Update stay off until it is set.
 *   - MYMORNINGNEWS_MOBILE_BUNDLE_ID: iOS bundle identifier and Android application id (default ai.mymorningnews.app).
 *   - MYMORNINGNEWS_MOBILE_EAS_OWNER: the Expo account that owns the project (default joel-christner).
 *   - GOOGLE_SERVICES_JSON: path to google-services.json for Android push through FCM. EAS builds read it from the
 *     EAS file variable GOOGLE_SERVICES_JSON; local builds use ./google-services.json when present (git-ignored).
 *   - updates/certificate.pem: the code signing certificate for EAS Update (scripts/generate-updates-certificate.js).
 *     Without it, updates are disabled and a warning is printed.
 *   - The Apple team id and store credentials live in eas.json and EAS credentials, never here.
 *
 * On-device testing with a free personal Apple team: set MYMORNINGNEWS_MOBILE_LOCAL_TEAM_ID to the personal team id.
 * The build then uses the bundle id <BUNDLE_ID>.dev (so the personal team never registers the store id) and drops
 * the push and associated domains entitlements, which personal teams cannot sign. Never used for store builds.
 *
 * Copyright (c) 2026 Lattice AI LLC.
 */
const fs = require('fs');
const path = require('path');
const { withEntitlementsPlist } = require('expo/config-plugins');
const { updatesConfig } = require('./scripts/updates');

function env(name) {
  return (process.env[name] || '').trim();
}

const BUNDLE_ID = env('MYMORNINGNEWS_MOBILE_BUNDLE_ID') || 'ai.mymorningnews.app';
// PLACEHOLDER: no EAS project exists yet. Set MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID (or paste the id here) after
// `npx eas-cli@latest init`.
const EAS_PROJECT_ID = env('MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID') || '';
const EAS_OWNER = env('MYMORNINGNEWS_MOBILE_EAS_OWNER') || 'joel-christner';
const LOCAL_GOOGLE_SERVICES = path.join(__dirname, 'google-services.json');
const GOOGLE_SERVICES_JSON = env('GOOGLE_SERVICES_JSON') || (fs.existsSync(LOCAL_GOOGLE_SERVICES) ? './google-services.json' : '');
const LOCAL_TEAM_ID = env('MYMORNINGNEWS_MOBILE_LOCAL_TEAM_ID');
const IOS_BUNDLE_ID = LOCAL_TEAM_ID ? BUNDLE_ID + '.dev' : BUNDLE_ID;

// Stores accept only numeric versions; the product's alpha label stays in extra.releaseVersion. Never change these
// without the owner's approval.
const RELEASE_VERSION = '0.1.0-alpha';
const STORE_VERSION = '0.1.0';
const BUILD_NUMBER = 1;

const ASSOCIATED_DOMAIN = 'app.mymorningnews.ai';
const BRAND_ORANGE = '#f5820d';
const SPLASH_LIGHT = '#ffffff';
const SPLASH_DARK = '#121417';
const FACE_ID_USAGE = 'My Morning News uses Face ID to unlock the app when you turn on the app lock.';

const warned = new Set();
function buildWarning(message) {
  if (!message || warned.has(message) || process.env.JEST_WORKER_ID) return;
  warned.add(message);
  process.stderr.write('[mymorningnews] Warning: ' + message + '\n');
}

/** Personal teams cannot sign push or associated domains; drop both for local device builds. */
function withoutPaidEntitlements(config) {
  return withEntitlementsPlist(config, (mod) => {
    delete mod.modResults['aps-environment'];
    delete mod.modResults['com.apple.developer.associated-domains'];
    return mod;
  });
}

module.exports = () => {
  const live = updatesConfig({ projectRoot: __dirname, projectId: EAS_PROJECT_ID });
  buildWarning(live.warning);

  const config = {
    name: 'My Morning News',
    slug: 'mymorningnews',
    owner: EAS_OWNER,
    platforms: ['ios', 'android'],
    version: STORE_VERSION,
    scheme: 'mymorningnews',
    orientation: 'default',
    icon: './assets/images/icon.png',
    userInterfaceStyle: 'automatic',
    runtimeVersion: live.runtimeVersion,
    updates: live.updates,
    ios: {
      bundleIdentifier: IOS_BUNDLE_ID,
      ...(LOCAL_TEAM_ID ? { appleTeamId: LOCAL_TEAM_ID } : null),
      supportsTablet: true,
      buildNumber: String(BUILD_NUMBER),
      // HTTPS only (exempt encryption), so App Store Connect skips the export compliance prompt.
      config: { usesNonExemptEncryption: false },
      associatedDomains: LOCAL_TEAM_ID ? [] : ['applinks:' + ASSOCIATED_DOMAIN],
      infoPlist: {
        NSFaceIDUsageDescription: FACE_ID_USAGE,
        UIBackgroundModes: ['audio', 'remote-notification'],
        // The hosted service is HTTPS. Plain HTTP is allowed only for local development and self-hosted servers on a
        // LAN; the app warns whenever the server URL is http://.
        NSAppTransportSecurity: { NSAllowsLocalNetworking: true },
      },
    },
    android: {
      package: BUNDLE_ID,
      versionCode: BUILD_NUMBER,
      ...(GOOGLE_SERVICES_JSON ? { googleServicesFile: GOOGLE_SERVICES_JSON } : null),
      adaptiveIcon: {
        backgroundColor: SPLASH_LIGHT,
        foregroundImage: './assets/images/adaptive-foreground.png',
        backgroundImage: './assets/images/adaptive-background.png',
        monochromeImage: './assets/images/adaptive-monochrome.png',
      },
      permissions: [
        'android.permission.USE_BIOMETRIC',
        'android.permission.USE_FINGERPRINT',
        'android.permission.POST_NOTIFICATIONS',
        'android.permission.FOREGROUND_SERVICE',
        'android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK',
      ],
      // The player never records; expo-audio adds RECORD_AUDIO by default, so it is removed here and in the plugin.
      blockedPermissions: ['android.permission.RECORD_AUDIO'],
      intentFilters: [
        {
          action: 'VIEW',
          autoVerify: true,
          data: [
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/briefings' },
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/topics' },
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/predictions' },
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/findings' },
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/suggestions' },
            { scheme: 'https', host: ASSOCIATED_DOMAIN, pathPrefix: '/dashboard' },
          ],
          category: ['BROWSABLE', 'DEFAULT'],
        },
      ],
    },
    plugins: [
      'expo-router',
      [
        'expo-splash-screen',
        {
          backgroundColor: SPLASH_LIGHT,
          image: './assets/images/splash-icon.png',
          imageWidth: 160,
          resizeMode: 'contain',
          dark: { backgroundColor: SPLASH_DARK, image: './assets/images/splash-icon-dark.png' },
        },
      ],
      ['expo-secure-store', { faceIDPermission: FACE_ID_USAGE }],
      ['expo-local-authentication', { faceIDPermission: FACE_ID_USAGE }],
      // Arabic and the RTL pseudo-locale mirror the layout; without this flag iOS and Android ignore forceRTL.
      ['expo-localization', { supportsRTL: true }],
      // Background playback: UIBackgroundModes audio on iOS, and the media playback foreground service on Android.
      ['expo-audio', { microphonePermission: false, recordAudioAndroid: false, enableBackgroundPlayback: true }],
      [
        'expo-notifications',
        {
          icon: './assets/images/notification-icon.png',
          color: BRAND_ORANGE,
          defaultChannel: 'briefings',
          enableBackgroundRemoteNotifications: true,
        },
      ],
      'expo-font',
      'expo-asset',
      'expo-web-browser',
      'expo-updates',
    ],
    extra: {
      releaseVersion: RELEASE_VERSION,
      associatedDomain: ASSOCIATED_DOMAIN,
      updates: live.extra,
      // Expo Go reads this to allow I18nManager.forceRTL.
      supportsRTL: true,
      ...(EAS_PROJECT_ID ? { eas: { projectId: EAS_PROJECT_ID } } : null),
    },
  };
  return LOCAL_TEAM_ID ? withoutPaidEntitlements(config) : config;
};
