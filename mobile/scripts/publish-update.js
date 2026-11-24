#!/usr/bin/env node
/**
 * Publish a code-signed over-the-air update with EAS Update.
 *
 * Usage (from mobile/):
 *   npm run publish-update -- --channel preview --message "Fix the usage chart"                  # dry run
 *   npm run publish-update -- --channel production --message "..." --platform ios --publish
 *   npm run publish-update -- --channel production --message "..." --rollout 10 --publish
 *
 * Options:
 *   --channel <name>     production, preview, or development (required)
 *   --message <text>     Update message shown in EAS (required)
 *   --platform <p>       ios, android, or all (default all)
 *   --rollout <percent>  Roll out to this share of users first, 1 to 100 (default 100)
 *   --key <file>         Private key (default ~/.mymorningnews/keys/updates/private-key.pem)
 *   --publish            Actually publish. Without it the command is only printed.
 *
 * Checks first that the code signing certificate exists (updates/certificate.pem), the private key exists, and an
 * EAS project id is configured. Publish with the same environment variables as the EAS environment of the target
 * binaries (MYMORNINGNEWS_MOBILE_*, GOOGLE_SERVICES_JSON), or the fingerprint runtime version will not match and the
 * update will reach no one. JavaScript-only changes can ship this way; native changes (new modules, permissions,
 * config plugins) always need a new store build.
 */
const childProcess = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const projectRoot = path.resolve(__dirname, '..');
const Channels = ['production', 'preview', 'development'];
const Platforms = ['ios', 'android', 'all'];

function parseArgs(argv) {
  const args = { channel: '', message: '', platform: 'all', rollout: 100, key: path.join(os.homedir(), '.mymorningnews', 'keys', 'updates', 'private-key.pem'), publish: false };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    const next = () => {
      i += 1;
      if (i >= argv.length) throw new Error(arg + ' needs a value.');
      return argv[i];
    };
    if (arg === '--channel') args.channel = next();
    else if (arg === '--message') args.message = next();
    else if (arg === '--platform') args.platform = next();
    else if (arg === '--rollout') args.rollout = Number(next());
    else if (arg === '--key') args.key = path.resolve(next());
    else if (arg === '--publish') args.publish = true;
    else throw new Error('Unknown option ' + arg);
  }
  if (!Channels.includes(args.channel)) throw new Error('--channel must be one of ' + Channels.join(', ') + '.');
  if (!args.message.trim()) throw new Error('--message is required.');
  if (!Platforms.includes(args.platform)) throw new Error('--platform must be one of ' + Platforms.join(', ') + '.');
  if (!Number.isInteger(args.rollout) || args.rollout < 1 || args.rollout > 100) throw new Error('--rollout must be a whole number from 1 to 100.');
  return args;
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const problems = [];
  if (!fs.existsSync(path.join(projectRoot, 'updates', 'certificate.pem'))) problems.push('No updates/certificate.pem. Run npm run updates-certificate first.');
  if (!fs.existsSync(args.key)) problems.push('No private key at ' + args.key + '.');
  if (!(process.env.MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID || '').trim()) problems.push('MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID is not set.');
  const command = ['eas-cli@latest', 'update', '--channel', args.channel, '--message', args.message, '--platform', args.platform, '--environment', args.channel, '--private-key-path', args.key, '--non-interactive'];
  if (args.rollout < 100) command.push('--rollout-percentage', String(args.rollout));
  process.stdout.write('npx ' + command.map((part) => (/\s/.test(part) ? JSON.stringify(part) : part)).join(' ') + '\n');
  if (problems.length) {
    for (const problem of problems) process.stderr.write('Not ready: ' + problem + '\n');
    if (args.publish) process.exit(1);
  }
  if (!args.publish) {
    process.stdout.write('Dry run. Add --publish to publish.\n');
    return;
  }
  const result = childProcess.spawnSync('npx', command, { cwd: projectRoot, stdio: 'inherit', env: { ...process.env, APP_VARIANT: args.channel } });
  process.exit(result.status === null ? 1 : result.status);
}

try {
  main();
} catch (error) {
  process.stderr.write(error.message + '\n');
  process.exit(1);
}
