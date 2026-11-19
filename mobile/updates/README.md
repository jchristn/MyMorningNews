# Over-the-air update signing

This folder holds the **public** code signing certificate for over-the-air updates, `certificate.pem`. Every build
embeds it, and `expo-updates` refuses any update whose signature does not verify against it. It is not generated yet:
the owner creates it once, after the EAS project exists (see `../OWNER_ACTIONS.md`).

```bash
npm run updates-certificate                          # writes updates/certificate.pem and ~/.mymorningnews/keys/updates/
```

The **private key never goes in this repository.** The script refuses to write it anywhere inside the Git work tree.
Keep the key in the owner's password manager and on the machine that publishes updates.

Until `certificate.pem` exists and `MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID` is set, builds have updates disabled and
print `[mymorningnews] Warning: Over-the-air updates are disabled ...`. That is the expected state for local
development.

## How it is wired

| Setting | Value |
| --- | --- |
| `updates.url` | `https://u.expo.dev/<EAS project id>` |
| `updates.codeSigningCertificate` | `./updates/certificate.pem` |
| `updates.codeSigningMetadata` | `{ keyid: "main", alg: "rsa-v1_5-sha256" }` |
| `runtimeVersion` | `{ policy: "fingerprint" }`, so an update only reaches binaries with the same native code |
| Channels | `development`, `preview`, `production`, set per build profile in `eas.json` |
| Publishing | `npm run publish-update -- --channel <c> --message "..." --publish` (`scripts/publish-update.js`, dry run without `--publish`) |

## Key rotation

`expo-updates` embeds exactly one certificate and one key id per binary. EAS Update signs an update with the single
private key passed to `eas update --private-key-path` and sends no certificate chain, so TrellisKit's standby key
scheme (which relies on its own update server sending a chain) does not apply. Rotation needs a new binary:

1. Generate a new pair into a new key directory: `npm run updates-certificate -- --key-dir ~/.mymorningnews/keys/updates-2 --force`.
   This replaces `certificate.pem`.
2. Ship a store build with the new certificate. Its fingerprint (runtime version) differs from the old binaries'.
3. Until the old binaries are gone (raise `minimumAppVersion` in the server's client config when contract D is live),
   publish each update twice: once from the commit that still has the old certificate with the old key, and once
   with the new key. Each reaches only the binaries whose runtime version matches.
4. When no supported binary embeds the old certificate, delete the old private key.

If a private key leaks, rotate immediately and raise the minimum app version so old binaries ask for a store update;
any binary that embeds the leaked certificate can be sent updates signed with that key until it is replaced.
