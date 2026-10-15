<#
.SYNOPSIS
    Joins the demo room from your PC's browser as a second participant.

.DESCRIPTION
    Generates a token with the app's own LiveKitTokenGenerator and DemoConfig
    (same server, key and secret), then opens LiveKit Meet's custom-connection page,
    which connects straight to the room.

    Build the LiveKitDemo (shared) project first so its DLL exists.

.EXAMPLE
    tools\join-browser.ps1
    tools\join-browser.ps1 -Room my-room -Name laptop
#>
param(
    [string]$Room = 'test-room',
    [string]$Name = "browser-$(Get-Random -Minimum 1000 -Maximum 9999)",
    [int]$Hours = 6,
    # Print the link instead of opening it.
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot '..\LiveKitDemo\LiveKitDemo\bin\Debug\netstandard2.0\LiveKitDemo.dll'
if (-not (Test-Path $dll)) { throw "Build the LiveKitDemo project first ($dll not found)." }

$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $dll).Path)
$cfg = $asm.GetType('LiveKitDemo.DemoConfig')
$url = $cfg.GetField('ServerUrl').GetValue($null)
$key = $cfg.GetProperty('ApiKey').GetValue($null)
$secret = $cfg.GetProperty('ApiSecret').GetValue($null)
if (-not $secret) { throw 'No API secret configured. Create LiveKitDemo\LiveKitDemo\DemoConfig.Local.cs (see the .example file) and rebuild.' }

$token = $asm.GetType('LiveKitDemo.Services.LiveKitTokenGenerator').GetMethod('Create').Invoke(
    $null, @($key, $secret, $Room, $Name, $Name, [TimeSpan]::FromHours($Hours)))

$link = 'https://meet.livekit.io/custom?liveKitUrl=' + [Uri]::EscapeDataString($url) +
        '&token=' + [Uri]::EscapeDataString($token)

Write-Host "Joining room '$Room' as '$Name' on $url"
if ($NoOpen) { Write-Host $link } else { Start-Process $link }
