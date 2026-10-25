# Build and test

Use a prebuilt release if you only want to use the app. These instructions are for Windows x64 development.

## Prerequisites

- .NET SDK 10.0 or newer for the companion.
- **PowerShell 7**, invoked as `pwsh`, for the APK build. Windows PowerShell 5.1 can run the release USB/install helpers, but the APK script uses modern .NET cryptography APIs.
- JDK 17, Android platform 35, and Android build-tools 35.0.0 for the APK.
- Node.js 22+ and Chrome for the optional integration tests.

No Gradle or Maven dependencies are required. The Android wrapper uses platform APIs; its web interface and dependencies are vendored locally.

## Clone and publish Windows

```powershell
git clone https://github.com/tugcantopaloglu/tablet-deck-studio.git
Set-Location tablet-deck-studio
dotnet publish .\companion\TabletDeck.csproj -c Release -r win-x64 --self-contained true -o .\dist\Windows
.\dist\Windows\TabletDeck-Companion.exe --self-test
```

Keep the published `wwwroot` and `Sounds` folders beside the executable. Twenty self-tests cover defaults, included sound assets, validation, key mapping, OBS authentication, confirmation propagation, and silent Windows WAV playback/stop.

Run the companion without `--self-test` to open the PC editor. Developer options are `--no-browser`, `--data-dir PATH`, `--http-port NUMBER`, and `--https-port NUMBER`. The shipping Android app uses HTTPS port 8743; alternate ports are for isolated development tests.

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

App identity: `app.tabletdeck.studio`, version `1.1.0`, version code `2`, minimum SDK `26`, target SDK `35`. Increment version code for later releases.

On first build, the script generates `toolchain/signing/tabletdeck.jks` and a random password file. Keep these private and preserve them to update your own APK installation later. Git ignores them. The original release's private signing key is not in this repository; a newly generated key produces a different identity and cannot update the original APK without uninstalling it first.

## Assemble the full package

Place the APK directly under `dist` and publish the companion to `dist/Windows`. Copy `USB-Connect.cmd`, `USB-Connect.ps1`, `Install-APK.cmd`, and `Install-APK.ps1` from `scripts` into `dist/Windows`.

Copy `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`, and `NOTICE.txt` from the verified platform-tools archive into `dist/Windows/platform-tools`. Preserve the included font, DOMPurify, icon, Android-tool, and Microsoft runtime notices when distributing those components.

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

The 44 checks cover pairing, lockout, revocation, editor isolation, OBS actions, audio, macros, saves, cancellation, disconnect state, browser errors, and layout constraints. Results and screenshots are written to ignored `.test-output`. Close the test companion using Ctrl+C afterward.

Never publish `.test-output/data`: it contains runtime editor/device credentials even though the OBS service and its password are test fixtures. Passing this suite does not verify physical Android installation, WebView, Wi-Fi, or USB operation.

## Security checks

```powershell
npx snyk code test .
npm audit --prefix .\tests
```

Snyk authentication may be required. `.dcignore` excludes generated builds and separately audited third-party code. The initial scan retains the informational `java/JavaScriptEnabled` note for the bundled Android interface. That does not justify loading arbitrary remote pages into its native bridge.

## Architecture

Android bundled UI → restricted Java bridge → certificate-pinned HTTPS → Windows ASP.NET Core companion → authenticated OBS v5 on loopback or a configured Windows action.

USB adds an ADB reverse tunnel for the same HTTPS port. The PC editor uses a separate authenticated loopback endpoint. Tablet action requests contain configured action IDs; PC targets are edited locally. Preserve this boundary when adding features.
