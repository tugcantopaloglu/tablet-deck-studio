param([string]$CompanionPath=(Join-Path $PSScriptRoot 'TabletDeck-Companion.exe'),[switch]$DryRun)
$ErrorActionPreference='Stop'
$taskExecutable=(Resolve-Path -LiteralPath $CompanionPath).Path
if([IO.Path]::GetFileName($taskExecutable) -ne 'TabletDeck-Companion.exe'){throw 'Select the Tablet Deck companion executable.'}
$taskBlocks=@(Get-NetFirewallApplicationFilter -Program $taskExecutable -ErrorAction SilentlyContinue | Get-NetFirewallRule | Where-Object {$_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Block'})
Write-Output ('Companion: '+$taskExecutable)
Write-Output ('Matching inbound block rules to disable: '+$taskBlocks.Count)
Write-Output 'Allow TCP 8743 and 8744 from LocalSubnet only, on Private/Public profiles. No internet port forwarding is configured.'
if($DryRun){Write-Output 'Dry run: firewall unchanged.';return}
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Windows requires administrator rights. Run this script from an Administrator PowerShell window.'}
$taskHash=[Security.Cryptography.SHA256]::Create()
try{$taskSuffix=([BitConverter]::ToString($taskHash.ComputeHash([Text.Encoding]::UTF8.GetBytes($taskExecutable.ToUpperInvariant())))).Replace('-','').Substring(0,16)}finally{$taskHash.Dispose()}
$taskRule='TabletDeck-Local-'+$taskSuffix
if(!(Get-NetFirewallRule -Name $taskRule -ErrorAction SilentlyContinue)){
    New-NetFirewallRule -Name $taskRule -DisplayName 'Tablet Deck local tablet control' -Direction Inbound -Action Allow -Program $taskExecutable -Protocol TCP -LocalPort 8743,8744 -RemoteAddress LocalSubnet -Profile Private,Public -EdgeTraversalPolicy Block | Out-Null
}
foreach($taskBlock in $taskBlocks){$taskBlock | Disable-NetFirewallRule | Out-Null}
Write-Output 'Local tablet Wi-Fi access configured. Keep the companion running and use its physical LAN address.'
