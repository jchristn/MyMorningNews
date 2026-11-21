#!/usr/bin/env node
/**
 * Generate the code signing key pair and certificate for over-the-air updates (expo-updates with EAS Update).
 *
 * Files:
 *   mobile/updates/certificate.pem   public certificate, embedded in every build. Commit it.
 *   <key dir>/private-key.pem        private key. NEVER in the repository. Default ~/.mymorningnews/keys/updates,
 *   <key dir>/public-key.pem         mode 0600. Store a copy in the owner's password manager.
 *
 * Usage (from mobile/):
 *   npm run updates-certificate                         # default key directory
 *   npm run updates-certificate -- --key-dir /secure/place
 *   npm run updates-certificate -- --years 5 --force
 *
 * Options:
 *   --key-dir <dir>   Where the private and public keys go (refused when inside this Git work tree)
 *   --years <n>       Certificate validity, 1 to 30 (default 10)
 *   --force           Overwrite an existing certificate or key
 *
 * It wraps `npx expo-updates codesigning:generate`. Equivalent openssl commands (OpenSSL 1.1.1+):
 *   openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 3650 -subj "/CN=Lattice AI LLC" \
 *     -addext "keyUsage=critical,digitalSignature" -addext "extendedKeyUsage=critical,codeSigning" \
 *     -keyout private-key.pem -out certificate.pem
 *
 * Rotation: see updates/README.md. A new certificate needs a new store binary; keep the old private key until every
 * binary that embeds the old certificate is gone.
 */
const childProcess = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const projectRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(projectRoot, '..');
const certificateDir = path.join(projectRoot, 'updates');

function parseArgs(argv) {
  const args = { keyDir: path.join(os.homedir(), '.mymorningnews', 'keys', 'updates'), years: 10, force: false };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    const next = () => {
      i += 1;
      if (i >= argv.length) throw new Error(arg + ' needs a value.');
      return argv[i];
    };
    if (arg === '--force') args.force = true;
    else if (arg === '--key-dir') args.keyDir = path.resolve(next());
    else if (arg === '--years') args.years = Number(next());
    else throw new Error('Unknown option ' + arg);
  }
  if (!Number.isInteger(args.years) || args.years < 1 || args.years > 30) throw new Error('--years must be a whole number from 1 to 30.');
  return args;
}

function isInside(child, parent) {
  const relative = path.relative(parent, child);
  return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (isInside(args.keyDir, repoRoot)) throw new Error('Refusing to write private keys inside the repository (' + args.keyDir + '). Choose a --key-dir outside it.');
  const certificate = path.join(certificateDir, 'certificate.pem');
  const privateKey = path.join(args.keyDir, 'private-key.pem');
  for (const file of [certificate, privateKey]) {
    if (fs.existsSync(file) && !args.force) throw new Error(file + ' already exists. Pass --force to replace it (this breaks updates for binaries that embed the old certificate).');
  }
  fs.mkdirSync(args.keyDir, { recursive: true, mode: 0o700 });
  fs.mkdirSync(certificateDir, { recursive: true });
  // expo-updates needs empty output directories, and updates/ holds a README, so generate into fresh temporary
  // directories and move the results into place.
  const certificateTemp = fs.mkdtempSync(path.join(os.tmpdir(), 'mmn-updates-cert-'));
  const keyTemp = fs.mkdtempSync(path.join(os.tmpdir(), 'mmn-updates-key-'));
  fs.chmodSync(keyTemp, 0o700);
  const result = childProcess.spawnSync(
    'npx',
    [
      'expo-updates',
      'codesigning:generate',
      '--key-output-directory',
      keyTemp,
      '--certificate-output-directory',
      certificateTemp,
      '--certificate-validity-duration-years',
      String(args.years),
      '--certificate-common-name',
      'Lattice AI LLC',
    ],
    { cwd: projectRoot, stdio: 'inherit' },
  );
  if (result.status !== 0) {
    fs.rmSync(certificateTemp, { recursive: true, force: true });
    fs.rmSync(keyTemp, { recursive: true, force: true });
    throw new Error('expo-updates codesigning:generate failed.');
  }
  fs.copyFileSync(path.join(certificateTemp, 'certificate.pem'), certificate);
  for (const name of fs.readdirSync(keyTemp)) {
    fs.copyFileSync(path.join(keyTemp, name), path.join(args.keyDir, name));
    fs.chmodSync(path.join(args.keyDir, name), 0o600);
  }
  fs.rmSync(certificateTemp, { recursive: true, force: true });
  fs.rmSync(keyTemp, { recursive: true, force: true });
  process.stdout.write('\nCertificate: ' + path.relative(process.cwd(), certificate) + ' (commit it)\n');
  process.stdout.write('Private key: ' + privateKey + ' (never commit it; back it up in the password manager)\n');
}

try {
  main();
} catch (error) {
  process.stderr.write(error.message + '\n');
  process.exit(1);
}
