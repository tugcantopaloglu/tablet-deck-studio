param([Parameter(Mandatory=$true)][string]$Toolchain,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskTools=(Resolve-Path -LiteralPath $Toolchain).Path
$taskJdk=(Get-ChildItem -LiteralPath (Join-Path $taskTools 'jdk') -Directory | Select-Object -First 1).FullName
$taskBuildTools=Join-Path $taskTools 'sdk\android-15'
$taskPlatform=Join-Path $taskTools 'sdk\android-35\android.jar'
$taskAndroid=Join-Path $taskRoot 'android'
$taskBuild=Join-Path $taskRoot 'build\android'
if(!$OutputDirectory){$OutputDirectory=Join-Path $taskRoot 'dist'}
New-Item -ItemType Directory -Force -Path $taskBuild,(Join-Path $taskBuild 'classes'),(Join-Path $taskBuild 'dex'),(Join-Path $taskBuild 'assets\www'),$OutputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\index.html'),(Join-Path $taskRoot 'companion\wwwroot\app.js'),(Join-Path $taskRoot 'companion\wwwroot\style.css') -Destination (Join-Path $taskBuild 'assets\www')
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\features.js') -Destination (Join-Path $taskBuild 'assets\www')
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\hardening.js') -Destination (Join-Path $taskBuild 'assets\www')
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\latency.js') -Destination (Join-Path $taskBuild 'assets\www')
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\fonts') -Destination (Join-Path $taskBuild 'assets\www') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'companion\wwwroot\vendor') -Destination (Join-Path $taskBuild 'assets\www') -Recurse -Force
function Invoke-Checked([string]$Exe,[string[]]$Arguments){& $Exe @Arguments;if($LASTEXITCODE -ne 0){throw "Build command failed: $Exe"}}
Invoke-Checked (Join-Path $taskBuildTools 'aapt2.exe') @('compile','--dir',(Join-Path $taskAndroid 'res'),'-o',(Join-Path $taskBuild 'resources.zip'))
Invoke-Checked (Join-Path $taskBuildTools 'aapt2.exe') @('link','-o',(Join-Path $taskBuild 'unsigned.apk'),'--manifest',(Join-Path $taskAndroid 'AndroidManifest.xml'),'-I',$taskPlatform,'-R',(Join-Path $taskBuild 'resources.zip'),'-A',(Join-Path $taskBuild 'assets'),'--auto-add-overlay')
$taskSources=@(Get-ChildItem -LiteralPath (Join-Path $taskAndroid 'src') -Filter '*.java' -Recurse | ForEach-Object FullName)
Invoke-Checked (Join-Path $taskJdk 'bin\javac.exe') (@('-encoding','UTF-8','--release','8','-classpath',$taskPlatform,'-d',(Join-Path $taskBuild 'classes'))+$taskSources)
Push-Location (Join-Path $taskBuild 'classes')
try {Invoke-Checked (Join-Path $taskJdk 'bin\jar.exe') @('cf',(Join-Path $taskBuild 'classes.jar'),'.')} finally {Pop-Location}
Invoke-Checked (Join-Path $taskJdk 'bin\java.exe') @('-cp',(Join-Path $taskBuildTools 'lib\d8.jar'),'com.android.tools.r8.D8','--release','--min-api','26','--lib',$taskPlatform,'--output',(Join-Path $taskBuild 'dex'),(Join-Path $taskBuild 'classes.jar'))
Push-Location (Join-Path $taskBuild 'dex')
try {Invoke-Checked (Join-Path $taskJdk 'bin\jar.exe') @('uf',(Join-Path $taskBuild 'unsigned.apk'),'classes.dex')} finally {Pop-Location}
Invoke-Checked (Join-Path $taskBuildTools 'zipalign.exe') @('-f','-p','4',(Join-Path $taskBuild 'unsigned.apk'),(Join-Path $taskBuild 'aligned.apk'))
$taskKeyDir=Join-Path $taskTools 'signing'
New-Item -ItemType Directory -Force -Path $taskKeyDir | Out-Null
$taskKey=Join-Path $taskKeyDir 'tabletdeck.jks'
$taskPasswordFile=Join-Path $taskKeyDir 'password.txt'
if(!(Test-Path -LiteralPath $taskKey)){
 $taskRandom=[byte[]]::new(32);[System.Security.Cryptography.RandomNumberGenerator]::Fill($taskRandom)
 [Convert]::ToHexString($taskRandom) | Set-Content -LiteralPath $taskPasswordFile -NoNewline
 Invoke-Checked (Join-Path $taskJdk 'bin\keytool.exe') @('-genkeypair','-keystore',$taskKey,'-storepass:file',$taskPasswordFile,'-keypass:file',$taskPasswordFile,'-alias','tabletdeck','-keyalg','RSA','-keysize','3072','-validity','3650','-dname','CN=Tablet Deck Personal Build','-noprompt')
}
$taskApk=Join-Path $OutputDirectory 'TabletDeck-Studio.apk'
Invoke-Checked (Join-Path $taskJdk 'bin\java.exe') @('-jar',(Join-Path $taskBuildTools 'lib\apksigner.jar'),'sign','--ks',$taskKey,'--ks-key-alias','tabletdeck','--ks-pass',('file:'+$taskPasswordFile),'--out',$taskApk,(Join-Path $taskBuild 'aligned.apk'))
Invoke-Checked (Join-Path $taskJdk 'bin\java.exe') @('-jar',(Join-Path $taskBuildTools 'lib\apksigner.jar'),'verify','--verbose',$taskApk)
Invoke-Checked (Join-Path $taskBuildTools 'aapt.exe') @('dump','badging',$taskApk)
Write-Output "APK built: $taskApk"
