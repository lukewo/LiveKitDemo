<#
.SYNOPSIS
    Generates a token with the app's own LiveKitTokenGenerator + DemoConfig and asks
    LiveKit Cloud to validate it. Confirms the key/secret and JWT signing are right
    without running the app. Build the LiveKitDemo project first.
#>
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot '..\LiveKitDemo\LiveKitDemo\bin\Debug\netstandard2.0\LiveKitDemo.dll'
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $dll).Path)
$cfg = $asm.GetType('LiveKitDemo.DemoConfig')
$url = $cfg.GetField('ServerUrl').GetValue($null)
$key = $cfg.GetProperty('ApiKey').GetValue($null)
$secret = $cfg.GetProperty('ApiSecret').GetValue($null)
if (-not $secret) { throw 'No API secret configured. Create LiveKitDemo\LiveKitDemo\DemoConfig.Local.cs (see the .example file) and rebuild.' }
$token = $asm.GetType('LiveKitDemo.Services.LiveKitTokenGenerator').GetMethod('Create').Invoke(
    $null, @($key, $secret, 'test-room', 'token-check', 'token-check', [TimeSpan]::FromMinutes(5)))

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$httpBase = $url -replace '^wss://', 'https://' -replace '^ws://', 'http://'

function Test-Token([string]$label, [string]$t) {
    $req = [Net.HttpWebRequest]::Create("$httpBase/rtc/validate?access_token=$t")
    try { $resp = $req.GetResponse() }
    catch [Net.WebException] {
        $resp = $_.Exception.Response
        if (-not $resp) { Write-Host "$label -> network error: $($_.Exception.Message)"; return }
    }
    $body = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd().Trim()
    Write-Host ("{0,-9} -> HTTP {1} {2}" -f $label, [int]$resp.StatusCode, $body)
    $resp.Close()
}

Write-Host "server: $url  key: $key"
Test-Token 'valid' $token
Test-Token 'tampered' ($token.Substring(0, $token.Length - 4) + 'AAAA')
