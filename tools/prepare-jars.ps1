<#
.SYNOPSIS
    Turns the raw Maven artifacts in LiveKitBindings\Jars\maven (downloaded by
    tools\fetch-livekit) into the layout LiveKitBindings.csproj consumes.

.DESCRIPTION
    Classic Xamarin.Android binding projects only support ONE LibraryProjectZip (.aar);
    multiple AARs get extracted over each other. So only livekit-android stays an AAR,
    and every dependency AAR is unpacked here into its classes jar + native libraries.

    Output layout (all regenerated on each run):

      Jars\bind\livekit-android-<v>.aar     LibraryProjectZip   bound + packaged
      Jars\bind\webrtc-<v>.jar              EmbeddedJar         bound + packaged
                                            (livekit.org.webrtc.* - SurfaceViewRenderer,
                                            VideoSink, etc. are part of LiveKit's API)
      Jars\embed\*.jar                      EmbeddedReferenceJar packaged, not bound
      Jars\native\<abi>\*.so                EmbeddedNativeLibrary packaged

    Not copied (supplied another way):
      - kotlin-stdlib*, kotlinx-coroutines-*, org.jetbrains:annotations
            -> official Microsoft NuGets (Xamarin.Kotlin.StdLib, Xamarin.KotlinX.Coroutines.*)
      - androidx.*, listenablefuture
            -> Xamarin.AndroidX.* NuGets that Xamarin.Forms already brings in
      - androidx.startup / androidx.profileinstaller
            -> only needed by androidx.lifecycle 2.6, which we don't ship
#>
[CmdletBinding()]
param([string]$JarsDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $JarsDir) {
    $root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $JarsDir = Join-Path $root '..\LiveKitBindings\Jars'
}
$JarsDir  = (Resolve-Path $JarsDir).Path
$mavenDir = Join-Path $JarsDir 'maven'
$bindDir  = Join-Path $JarsDir 'bind'
$embedDir = Join-Path $JarsDir 'embed'
$nativeDir = Join-Path $JarsDir 'native'

if (-not (Test-Path $mavenDir)) { throw "Missing $mavenDir. Run tools\fetch-livekit\gradlew fetch first." }

# ABIs Xamarin.Android supports. JNA also ships armeabi/mips/mips64, which are dropped.
$abis = 'arm64-v8a', 'armeabi-v7a', 'x86', 'x86_64'

# File-name prefixes of artifacts that are NOT packaged from raw files.
$skip = @(
    'kotlin-stdlib-', 'kotlinx-coroutines-', 'annotations-23',          # Microsoft Kotlin NuGets
    'annotation-', 'core-common-', 'core-runtime-', 'collection-',       # AndroidX via Xamarin.Forms
    'concurrent-futures-', 'core-1.', 'interpolator-', 'lifecycle-',
    'tracing-', 'versionedparcelable-', 'listenablefuture-',
    'startup-runtime-', 'profileinstaller-'                              # not needed
)

foreach ($d in $bindDir, $embedDir, $nativeDir) {
    if (Test-Path $d) { Remove-Item -Recurse -Force $d }
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

function Test-Skip([string]$name) {
    foreach ($p in $skip) { if ($name.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

function Save-Entry($entry, [string]$path) {
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path, $true)
}

$report = New-Object System.Collections.Generic.List[string]

foreach ($f in Get-ChildItem $mavenDir -File | Where-Object { $_.Extension -in '.aar', '.jar' } | Sort-Object Name) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($f.Name)

    if ($f.Name -like 'livekit-android-*.aar') {
        Copy-Item $f.FullName (Join-Path $bindDir $f.Name)
        $report.Add("bind    $($f.Name)")
        continue
    }
    if (Test-Skip $f.Name) { $report.Add("skip    $($f.Name)"); continue }

    if ($f.Extension -eq '.jar') {
        Copy-Item $f.FullName (Join-Path $embedDir $f.Name)
        $report.Add("embed   $($f.Name)")
        continue
    }

    # Dependency AAR: unpack bytecode jars and native libs.
    $isWebrtc = $f.Name -like 'android-prefixed-*'
    $zip = [IO.Compression.ZipFile]::OpenRead($f.FullName)
    try {
        foreach ($e in $zip.Entries) {
            $isJar = ($e.FullName -eq 'classes.jar' -or $e.FullName -like 'libs/*.jar') -and $e.Length -gt 64
            if ($isJar) {
                if ($isWebrtc) {
                    # webrtc keeps its real classes in libs/shadow-all.jar
                    $name = "webrtc-$($stem -replace '^android-prefixed-','').jar"
                    Save-Entry $e (Join-Path $bindDir $name)
                    $report.Add("bind    $name  (from $($f.Name)!$($e.FullName))")
                } else {
                    $suffix = if ($e.FullName -eq 'classes.jar') { '' } else { '-' + [IO.Path]::GetFileNameWithoutExtension($e.Name) }
                    $name = "$stem$suffix.jar"
                    Save-Entry $e (Join-Path $embedDir $name)
                    $report.Add("embed   $name  (from $($f.Name))")
                }
            }
            elseif ($e.FullName -match '^jni/([^/]+)/([^/]+\.so)$' -and $abis -contains $Matches[1]) {
                Save-Entry $e (Join-Path $nativeDir "$($Matches[1])\$($Matches[2])")
                $report.Add("native  $($Matches[1])/$($Matches[2])  (from $($f.Name))")
            }
        }
    } finally { $zip.Dispose() }
}

$report | ForEach-Object { Write-Host $_ }
