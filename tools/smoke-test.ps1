<#
.SYNOPSIS
    Launches the installed demo app on a device/emulator and reports whether it
    stays alive, plus any fatal / LiveKit-related log lines. Saves a screenshot.
.EXAMPLE
    tools\smoke-test.ps1 -Serial emulator-5554
#>
param(
    [string]$Serial = 'emulator-5554',
    [string]$Package = 'com.companyname.livekitdemo',
    [int]$WaitSeconds = 25,
    [string]$Screenshot = (Join-Path $env:TEMP 'livekit-demo.png')
)

$adb = "${env:ProgramFiles(x86)}\Android\android-sdk\platform-tools\adb.exe"
function Adb { & $adb -s $Serial @args 2>&1 }

Adb logcat -c | Out-Null
Adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds $WaitSeconds

$appPid = ("" + (Adb shell pidof $Package)).Trim()
Write-Host "running: $([bool]$appPid)  pid: $appPid"

$log = Adb logcat -d -v brief
$interesting = $log | Select-String 'FATAL EXCEPTION|AndroidRuntime|UnsatisfiedLinkError|ClassNotFoundException|NoClassDefFoundError|NoSuchMethodError|NoSuchFieldError|Java\.Lang\.\w+Exception|System\.\w+Exception|monodroid.*(error|Error)'
Write-Host "fatal/exception lines: $(@($interesting).Count)"
$interesting | Select-Object -First 40 | ForEach-Object { Write-Host $_.Line }

Adb shell screencap -p /sdcard/livekit-demo.png | Out-Null
Adb pull /sdcard/livekit-demo.png $Screenshot | Out-Null
Adb shell rm /sdcard/livekit-demo.png | Out-Null
Write-Host "screenshot: $Screenshot"
