# Build and test

Use a prebuilt release if you only want to use the app. These instructions are for Windows x64 development.

## Prerequisites

- .NET SDK 10.0 or newer for the companion.
- **PowerShell 7**, invoked as `pwsh`, for the APK build. Windows PowerShell 5.1 can run the release USB/install helpers, but the APK script uses modern .NET cryptography APIs.
- JDK 17, Android platform 35, and Android build-tools 35.0.0 for the APK.
- Node.js 22+ and Chrome for the optional integration tests.

No Gradle or Maven dependencies are required. The Android wrapper uses platform APIs; its web interface and dependencies are vendored locally. The Windows build restores NAudio.Wasapi 2.2.1 and its dependencies from NuGet for audio-session control.

## Clone and publish Windows

```powershell
git clone https://github.com/tugcantopaloglu/tablet-deck-studio.git
Set-Location tablet-deck-studio
dotnet publish .\companion\TabletDeck.csproj -c Release -r win-x64 --self-contained true -o .\dist\Windows
.\dist\Windows\TabletDeck-Companion.exe --self-test
```

Keep the published `wwwroot`, `Sounds`, and `Licenses` folders beside the executable. Twenty-six self-tests cover defaults, included sound assets, validation, key mapping, OBS authentication, confirmations, Soundpad pipe/XML parsing, app-mixer validation, atomic layout reorder, and Soundpad button import.

The Android build packs bundled assets with the JDK `jar` tool. Windows `aapt2 -A` produced backslashes in asset ZIP entry names during the physical 1.4 test, which Android could not load. Before signing, the build rejects backslash entries and verifies every required UI asset has its exact forward-slash path. See [the hardware test report](LIVE-TEST.md).

An additional real Windows audio smoke test creates a silent playback session, adjusts only its own volume/mute, verifies those values, and restores them:

```powershell
.\dist\Windows\TabletDeck-Companion.exe --audio-smoke-test
```

Run the companion without `--self-test` to open the editor and tray. Use `--no-browser --no-tray` for server-only tests. Developer options are `--no-browser`, `--data-dir PATH`, `--http-port NUMBER`, and `--https-port NUMBER`. The shipping Android app uses HTTPS port 8743; alternate ports are for isolated development tests.

## Prepare the Android toolchain

Exact official archive URLs and SHA-256 values are in [toolchain-downloads.json](../scripts/toolchain-downloads.json). In PowerShell 7, from the repository root:

```powershell
$tools = Join-Path (Get-Location) 'toolchain'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$packages = Get-Content .\scripts\toolchain-downloads.json -Raw | ConvertFrom-Json
foreach ($package in $packages) {
    $name = [IO.Path]::GetFileName(([Uri]$package.url).AbsolutePath)
    $archive = Join-Path $tools $name
    Invoke-WebRequest -Uri $package.url -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLower() -ne $package.sha256) {
        throw "Checksum mismatch: $name"
    }
    $destination = Join-Path $tools $package.extractTo
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
}
```

Review the publishers' license terms for downloaded SDK tools. The initial archives extract to:

```text
toolchain/
├── jdk/jdk-17.../bin/java.exe
└── sdk/
    ├── android-15/aapt2.exe
    ├── android-15/zipalign.exe
    ├── android-15/lib/
    ├── android-35/android.jar
    └── platform-tools/adb.exe
```

Here `android-15` is the build-tools archive's directory name, not the minimum Android version. For an existing SDK installation, arrange these paths or adapt the two tool-path variables in `Build-Android.ps1`.

## Build and sign Android

```powershell
# PowerShell 7, from the repository root:
$tools = Join-Path (Get-Location) 'toolchain'
$output = Join-Path (Get-Location) 'dist'
.\scripts\Build-Android.ps1 -Toolchain $tools -OutputDirectory $output
```

The script compiles resources and Java, converts classes to DEX, packages the bundled interface, aligns the APK, signs it, and verifies the result. Output: `dist/TabletDeck-Studio.apk`.

App identity: `app.tabletdeck.studio`, version `1.4.0`, version code `5`, minimum SDK `26`, target SDK `35`. Increment version code for later releases.

On first build, the script generates `toolchain/signing/tabletdeck.jks` and a random password file. Keep these private and preserve them to update your own APK installation later. Git ignores them. The original release's private signing key is not in this repository; a newly generated key produces a different identity and cannot update the original APK without uninstalling it first.

## Assemble the full package

Place the APK directly under `dist` and publish the companion to `dist/Windows`. Copy `USB-Connect.cmd`, `USB-Connect.ps1`, `Install-APK.cmd`, and `Install-APK.ps1` from `scripts` into `dist/Windows`.

Copy `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`, and `NOTICE.txt` from the verified platform-tools archive into `dist/Windows/platform-tools`. Preserve the included font, DOMPurify, icon, NAudio, Android-tool, and Microsoft runtime notices when distributing those components.

Do not bundle runtime configuration, paired-device records, private certificates, or signing material.

## Run integration tests

The tests use a real companion with a synthetic OBS WebSocket v5 service. They do not operate your live OBS instance. Ports 18740, 18743, and 19455 must be free.

In terminal 1, from the repository root:

```powershell
$testData = Join-Path (Get-Location) '.test-output\data'
.\dist\Windows\TabletDeck-Companion.exe --no-browser --data-dir $testData --http-port 18740 --https-port 18743
```

In terminal 2, also from the repository root:

```powershell
npm ci --prefix .\tests
npm test --prefix .\tests
```

The default browser path is `C:\Program Files\Google\Chrome\Application\chrome.exe`. For another compatible Chromium executable, set `TABLETDECK_CHROME` before running:

```powershell
$env:TABLETDECK_CHROME = 'C:\Path\To\chrome.exe'
npm test --prefix .\tests
```

The 44 original checks cover pairing, lockout, revocation, editor isolation, OBS actions, audio, macros, saves, cancellation, disconnect state, browser errors, and layout constraints. Another 29 checks cover library/mixer APIs, validation, mouse/touch/keyboard reordering, save/cancel, search/categories, escaped titles, volume/mute, playback/import payloads, and layout bounds. `npm test` runs both suites. To run only the new suite, use `npm run test:features --prefix .\tests`.

Soundpad library and mixer UI tests use fixtures; actual layout endpoints and Windows session enumeration use the real companion. The optional audio smoke test exercises Windows volume writes only for its own process. Results and screenshots are written to ignored `.test-output`. Close the test companion using Ctrl+C afterward.

Never publish `.test-output/data`: it contains runtime editor/device credentials even though the OBS service and its password are test fixtures. Passing this suite does not verify physical Android installation, WebView, Wi-Fi, or USB operation.

## Security checks

```powershell
npx snyk code test .
npm audit --prefix .\tests
dotnet list .\companion\TabletDeck.csproj package --vulnerable --include-transitive
```

Snyk authentication may be required. `.dcignore` excludes generated builds and separately audited third-party code. The initial scan retains the informational `java/JavaScriptEnabled` note for the bundled Android interface. That does not justify loading arbitrary remote pages into its native bridge.

## Architecture

Android bundled UI → restricted Java bridge → pinned persistent TLS control (8744) or pinned HTTPS fallback/metadata (8743) → Windows companion → authenticated OBS v5 or a configured Windows action. PC panels receive authenticated state events.

USB adds ADB reverse tunnels for ports 8743 and 8744. The PC editor uses a separate authenticated loopback endpoint. Tablet action requests contain configured action IDs; PC targets are edited locally. Paired tablets can also reorder the exact existing button set, play library indices validated against Soundpad, and adjust enumerated audio sessions by opaque IDs. Soundpad button import remains local-admin only. Preserve these boundaries when adding features.

## Build the Windows installer

Assemble the complete release ZIP with SHA256SUMS.txt listing every file except itself, then embed it:

```powershell
$payload = (Resolve-Path .\dist\TabletDeck-Studio-1.5.zip).Path
dotnet publish .\installer\TabletDeck.Setup.csproj -c Release -o .\dist\Setup "-p:PayloadArchive=$payload"
.\dist\Setup\TabletDeck-Setup.exe --verify-payload
```

The installer uses a fixed per-user destination. `--test-mode --quiet` installs only to the fixed temporary `TabletDeckStudioSetupTests/TabletDeckStudio` folder, without shortcuts or registry changes. `--test-mode --uninstall` removes its managed test files. It refuses an existing unrecognized folder. Keep test mode away from any user-created folder with that name. Payload verification does not install or launch the app.

## Test without opening apps

```powershell
.\dist\Windows\TabletDeck-Companion.exe --self-test
.\dist\Windows\TabletDeck-Companion.exe --hardening-self-test
$env:TABLETDECK_COMPANION = (Resolve-Path .\dist\Windows\TabletDeck-Companion.exe).Path
npm run test:headless --prefix .\tests
npm run test:fast --prefix .\tests
```

For the original OBS integration suite, start an isolated companion on ports 18740/18743 using `--no-browser --no-tray`, then set `$env:TABLETDECK_HEADLESS='1'` and run `node .\tests\integration.cjs`. That mode executes API/OBS fixtures and skips all browser launches. Default `npm test` still includes browser tests; do not use it when browser execution is disallowed.

The safety tests use fake keyboard sinks and HTTP fixtures. They do not inject keys, write startup settings, open streaming apps, or control Voicemeeter. JavaScript logic tests run without a browser and do not verify layout or native Android touch behavior.

The platform-only Java fast-link test needs JDK17 via JAVA_HOME or the documented toolchain. See [performance measurements and benchmark commands](PERFORMANCE.md). Alternate --https-port also shifts the default fast port to the next integer; --control-port overrides it. Android release clients use the shipping 8743/8744 ports.
