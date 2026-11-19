# Mobile device testing

How to run the My Morning News app (`mobile/`) on iOS simulators, Android emulators, and physical phones and tablets
against a local server. Store builds and submission are in `APP_STORE_SUBMISSION.md`; the app itself is described in
`mobile/README.md`.

## Prerequisites

- Node.js 24 and npm (`cd mobile && npm ci`).
- The Docker stack running (`cd docker && docker compose up -d`), with the server on `http://127.0.0.1:12100`. Local
  sign-in: `admin@mymorningnews` / `password`. Leave the tenant empty; the server finds it (tenant-less sign-in).
- iOS: Xcode with an iOS simulator runtime (macOS only).
- Android: Android Studio (for the SDK at `~/Library/Android/sdk` and its bundled Java runtime) and an emulator image.
  Gradle needs Java: `export JAVA_HOME="/Applications/Android Studio.app/Contents/jbr/Contents/Home"`.
- Optional: [Maestro](https://maestro.mobile.dev) for the smoke flows in `mobile/e2e/` (it also needs `JAVA_HOME`).

`ios/` and `android/` are generated on the first `expo run:*` (Continuous Native Generation) and are git-ignored. After
changing `app.config.js` or adding a native module, regenerate them with `npx expo prebuild --clean`.

## 1. iOS simulator (iPhone)

```bash
cd mobile
npx expo run:ios --device "iPhone 18 Pro" --port 12191
```

This builds a debug app, installs it, and starts Metro on port 12191. On the sign-in screen tap **Change server**,
then **Use a local development server** (`http://127.0.0.1:12100`; the simulator shares the Mac's loopback). The
plain HTTP warning is expected.

## 2. iPad simulator

Install the same build on a tablet simulator (Metro from step 1 keeps serving it):

```bash
xcrun simctl boot "iPad Pro 11-inch (M5)"
xcrun simctl install "iPad Pro 11-inch (M5)" ~/Library/Developer/Xcode/DerivedData/MyMorningNews-*/Build/Products/Debug-iphonesimulator/MyMorningNews.app
xcrun simctl launch "iPad Pro 11-inch (M5)" ai.mymorningnews.app
```

Or run `npx expo run:ios --device "iPad Pro 11-inch (M5)" --port 12191` with Metro stopped. Windows 768 points and
wider show split views and the persistent mini player; rotate to check both orientations.

## 3. Android emulator (phone and tablet)

Create an emulator once (any recent Google APIs image), start it, and build:

```bash
export JAVA_HOME="/Applications/Android Studio.app/Contents/jbr/Contents/Home"
export ANDROID_HOME=~/Library/Android/sdk
$ANDROID_HOME/cmdline-tools/latest/bin/avdmanager create avd -n MyMorningNews_Phone -d pixel_9 \
  -k "system-images;android-36.1;google_apis_playstore;arm64-v8a"
$ANDROID_HOME/emulator/emulator -avd MyMorningNews_Phone -port 5580 &
cd mobile
npx expo run:android --device MyMorningNews_Phone --port 12191
```

In the app, **Change server** > **Use a local development server** gives `http://10.0.2.2:12100` (the emulator's
alias for the Mac's loopback). For a tablet, create a second AVD with `-d pixel_tablet`.

If Metro is already running on 12191 from another build, build with `--no-bundler` and forward the default port:
`adb -s emulator-5580 reverse tcp:8081 tcp:12191`.

## 4. Physical devices on your network

The phone reaches the server through your Mac's LAN address, for example `http://192.168.1.20:12100`
(`ipconfig getifaddr en0`). Make sure the macOS firewall lets Docker accept connections, then enter that URL under
**Change server**. Plain HTTP is allowed only for local networks (`NSAllowsLocalNetworking` on iOS); use HTTPS for
anything else.

### iPhone or iPad with a free personal team

Without the Lattice AI LLC Apple Developer team you can still install on your own device for seven days at a time:

1. In Xcode > Settings > Accounts, add your Apple ID; note the personal team ID (Membership details).
2. Build with the local team, which switches the bundle id to `ai.mymorningnews.app.dev` and drops the push and
   associated domains entitlements that personal teams cannot sign:

   ```bash
   cd mobile
   export MYMORNINGNEWS_MOBILE_LOCAL_TEAM_ID=<your personal team id>
   npx expo prebuild --clean --platform ios
   npx expo run:ios --device --port 12191      # pick the connected iPhone
   ```

3. On the device, trust the developer profile (Settings > General > VPN & Device Management) and enable Developer
   Mode if asked.

Push notifications and universal links do not work with a personal team; everything else does.

### Android phone or tablet

Enable Developer options and USB debugging, connect the device, and run `npx expo run:android --device --port 12191`.
`adb reverse tcp:12191 tcp:12191` lets the device reach Metro over USB.

## 5. What to check

| Area | Steps |
| --- | --- |
| Sign-in | Change server to the local one, sign in without a tenant, sign out (session revoked), sign in again. An account in several tenants shows the tenant picker. |
| Session | Leave the app for more than the session lifetime, or revoke the session in the dashboard: the app refreshes with the rotating refresh token, or says "Your session expired". |
| Account | Profile save, password change, Your usage chart, Notifications (permission asked only here; simulators report that push needs a device), Language (Arabic or "Pseudo RTL" mirrors the layout after "Restart now"), Appearance (light, dark, system), App lock (Face ID or passcode; Features > Face ID > Enrolled on the simulator), Server, About. |
| Player | Listen > Developer player (development builds only): Load test tone, play, back 15, forward 30, drag the scrubber, chapters, speed 0.75x to 2x, sleep timer. Then press Home or lock the device: audio continues, and the lock screen (iOS) or the media notification (Android) shows the title, artwork, play and pause, back 15, and forward 30. Leave it longer than three minutes on Android to prove the foreground service keeps it alive. |
| Layout | Phone portrait and landscape, tablet split views, the largest text size (Settings > Accessibility > Display & Text Size), VoiceOver or TalkBack labels. |
| Links | `xcrun simctl openurl booted mymorningnews://briefings/test` or `adb shell am start -a android.intent.action.VIEW -d "mymorningnews://topics/test"` opens the right screen. |

## 6. Smoke flows (Maestro)

Run from the git-ignored `mobile/qa/` folder so screenshots stay out of Git:

```bash
cd mobile/qa
maestro --device <simulator or emulator id> test -e SERVER=http://127.0.0.1:12100 ../e2e/sign-in-and-account.yaml
maestro --device <id> test ../e2e/player-test-tone.yaml && sleep 60 && maestro --device <id> test ../e2e/player-resume-check.yaml
maestro --device <tablet id> test ../e2e/tablet-split.yaml
maestro --device <id> test ../e2e/rtl-pseudo-locale.yaml
```

Use `SERVER=http://10.0.2.2:12100` on an Android emulator. Never commit screenshots: product screenshots are captured
only after the owner's interface review (plan section 34.1).

## Known local limitations

- Push notifications need a physical device, an EAS project ID, and (Android) Firebase credentials; simulators and
  emulators show "Push notifications need a physical device".
- Universal links need the hosted domain's association files; use the custom scheme locally.
- Over-the-air updates are off in development and until the update certificate and EAS project exist.
