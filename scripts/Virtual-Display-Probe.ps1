#requires -Version 5.1
<# .SYNOPSIS
Read installed display adapters and active desktop topology without changing either.
 .DESCRIPTION
This helper never installs a driver, creates a monitor, enables test signing, changes
display modes, starts an app, or requests elevation. Installed virtual adapters alone
do not prove that an independent tablet desktop is available.
 .EXAMPLE
.\scripts\Virtual-Display-Probe.ps1 -Json
#>
[CmdletBinding()]
param([switch]$Json)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Virtual display diagnostics require Windows.'
}

if (-not ('TabletDeck.Diagnostics.DisplayTopology' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'Virtual-Display.Native.cs')
}
$warnings = [Collections.Generic.List[string]]::new()
$paths = @()
$topologyRead = $false
try {
    $paths = @([TabletDeck.Diagnostics.DisplayTopology]::ReadActive())
    $topologyRead = $true
} catch {
    $warnings.Add($_.Exception.GetBaseException().Message)
}

$adapters = @()
try {
    $devices = @(Get-CimInstance Win32_VideoController)
    $signedDrivers = @(Get-CimInstance Win32_PnPSignedDriver -Filter "DeviceClass='DISPLAY'")
    $adapters = @($devices | ForEach-Object {
        $device = $_
        $driver = $signedDrivers | Where-Object { $_.DeviceID -eq $device.PNPDeviceID } | Select-Object -First 1
        [pscustomobject]@{
            name = $device.Name
            instanceId = $device.PNPDeviceID
            version = $device.DriverVersion
            inf = if ($driver) { $driver.InfName } else { $null }
            isSignedReportedByWindows = if ($driver) { $driver.IsSigned } else { $null }
            signer = if ($driver) { $driver.Signer } else { $null }
            virtualNameHint = $device.Name -match 'virtual|indirect|SudoVDA|SudoMaker'
        }
    })
} catch {
    $warnings.Add('Display adapter inventory failed: ' + $_.Exception.Message)
}

$uniqueSources = @($paths | Select-Object -ExpandProperty SourceKey -Unique)
$indirectSources = @($paths | Where-Object { $_.IndirectOutputHint -and -not $_.SharesDesktopSource -and $_.HasSourceMode })
$report = [pscustomobject]@{
    schemaVersion = 1
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    readOnly = $true
    topologyRead = $topologyRead
    activeDesktopSourceCount = $uniqueSources.Count
    hasIndependentDesktopSources = $topologyRead -and $uniqueSources.Count -gt 1
    activeIndirectSourceCount = $indirectSources.Count
    tabletExtendedDisplayVerified = $false
    status = if (-not $topologyRead) { 'topology-unavailable' } elseif ($indirectSources.Count -gt 0) { 'indirect-desktop-present-tablet-unverified' } elseif (@($adapters | Where-Object virtualNameHint).Count -gt 0) { 'virtual-adapter-present-no-active-indirect-desktop' } else { 'virtual-display-driver-required' }
    adapters = $adapters
    activePaths = $paths
    warnings = @($warnings.ToArray())
    nextStep = 'Review docs/EXTENDED-DISPLAY.md. Driver presence and multiple physical monitors do not verify a tablet display.'
}
if ($Json) { $report | ConvertTo-Json -Depth 8 } else { $report }
