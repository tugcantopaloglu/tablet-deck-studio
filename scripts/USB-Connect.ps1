param([string]$Serial)
$ErrorActionPreference='Stop'
try {
 $taskAdb=Join-Path $PSScriptRoot 'platform-tools\adb.exe'
 if(!(Test-Path -LiteralPath $taskAdb)){throw 'ADB is missing. Extract the complete Windows package before running this script.'}
 & $taskAdb start-server | Out-Null
 $taskLines=@(& $taskAdb devices)
 $taskDevices=@($taskLines | Where-Object {$_ -match '^\S+\s+device$'} | ForEach-Object {($_ -split '\s+')[0]})
 if($Serial){if($Serial -notin $taskDevices){throw 'That tablet is not connected and authorized.'};$taskDevice=$Serial}
 elseif($taskDevices.Count -eq 1){$taskDevice=$taskDevices[0]}
 elseif($taskDevices.Count -gt 1){throw 'Multiple Android devices are attached. Run USB-Connect.ps1 -Serial YOUR_DEVICE_SERIAL to choose one.'}
 else {throw 'No authorized Android device. Use a USB data cable, enable USB debugging in Developer options, unlock the tablet, and approve the PC. You may also need the tablet manufacturer USB driver.'}
 & $taskAdb -s $taskDevice reverse tcp:8743 tcp:8743
 if($LASTEXITCODE -ne 0){throw 'Could not create the USB connection.'}
 Write-Host 'USB connection is ready. Keep the Windows companion running.' -ForegroundColor Cyan
 Write-Host 'On the tablet choose USB. Pair using the certificate code and PIN shown in the Windows companion.'
 Write-Host 'Run this helper again after unplugging the cable or restarting the tablet.'
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
