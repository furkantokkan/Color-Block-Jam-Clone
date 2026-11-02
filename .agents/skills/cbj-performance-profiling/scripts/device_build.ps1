<#
Builds a copy-only Development APK for the device check. Never builds from the repo itself: the live Editor holds the
project open, and the probe, the build script and the level tweak must stay out of the repo.

  powershell -ExecutionPolicy Bypass -File device_build.ps1 [-Level 5] [-CopyName devbuild] [-Root <folder>] [-InputSystemOnly]

-Level          1-based level the APK opens (Game is the first scene; its installer index is set in the copy).
-Root           parent folder of the copy; default %USERPROFILE%\.agent-context\cbj-perf. Not %TEMP%: it was
                wiped during work on 2026-09-29, and other sessions delete %TEMP%\cbj-* folders.
-CopyName       folder under Root. Keep the same name to reuse the IL2CPP/Bee cache (later builds are faster).
-InputSystemOnly  sets Active Input Handling to the Input System in the copy (for testing before the repo has it).
-GpuBreakdown   puts DevGpuBreakdown (render-setting A/B, no touches needed) in the APK instead of DevPerfProbe.
-OutlineAB      makes DevPerfProbe hide the outline on every second touch (tell the tester first: it looks like a bug).
-FromInit       starts the APK at Init like the real game (Init -> Hub -> Game) instead of opening the level.
Prints the DEVBUILD lines of the log and the APK path. The first IL2CPP ARM64 build took about 10 minutes.
#>
param(
    [int]$Level = 5,
    [string]$Root = (Join-Path $env:USERPROFILE '.agent-context\cbj-perf'),
    [string]$CopyName = 'devbuild',
    [switch]$InputSystemOnly,
    [switch]$GpuBreakdown,
    [switch]$OutlineAB,
    [switch]$FromInit
)

$ErrorActionPreference = 'Stop'
$skill = Split-Path -Parent $PSScriptRoot
$repo = (Resolve-Path (Join-Path $skill '..\..\..')).Path
New-Item -ItemType Directory -Force $Root | Out-Null
$copy = Join-Path $Root $CopyName

Write-Host "Mirroring $repo -> $copy"
# Excluded folders are neither copied nor purged, so the copy keeps its own Library\Bee cache between builds.
robocopy $repo $copy /MIR /R:0 /W:0 /NFL /NDL /NP /MT:16 /XD Temp Logs obj .plastic .vs Builds (Join-Path $repo 'Library\Bee') | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

# Copy-only probe (one of the two) and build script.
$probeFolder = Join-Path $copy 'Assets\DevProbe'
New-Item -ItemType Directory -Force (Join-Path $probeFolder 'Editor') | Out-Null
Remove-Item (Join-Path $probeFolder 'DevPerfProbe.cs*'), (Join-Path $probeFolder 'DevGpuBreakdown.cs*') -Force -ErrorAction SilentlyContinue
$probe = if ($GpuBreakdown) { 'DevGpuBreakdown.cs' } else { 'DevPerfProbe.cs' }
Copy-Item (Join-Path $skill "assets\device\$probe") (Join-Path $probeFolder $probe) -Force
if ($OutlineAB -and -not $GpuBreakdown) {
    $probePath = Join-Path $probeFolder $probe
    (Get-Content $probePath -Raw) -replace 's_outlineAB = false', 's_outlineAB = true' | Set-Content $probePath -NoNewline
}
Copy-Item (Join-Path $skill 'assets\device\DevBuild.cs') (Join-Path $probeFolder 'Editor\DevBuild.cs') -Force

# Copy-only settings: the level the APK opens, GPU frame timing, optionally Input System only.
$game = Join-Path $copy 'Assets\ColorBlockJam\Scenes\Game.unity'
(Get-Content $game -Raw) -replace '(?m)^  m_levelIndex: \d+$', "  m_levelIndex: $($Level - 1)" | Set-Content $game -NoNewline
$settings = Join-Path $copy 'ProjectSettings\ProjectSettings.asset'
$text = (Get-Content $settings -Raw) -replace '(?m)^  enableFrameTimingStats: 0$', '  enableFrameTimingStats: 1'
if ($InputSystemOnly) { $text = $text -replace '(?m)^  activeInputHandler: \d$', '  activeInputHandler: 1' }
Set-Content $settings $text -NoNewline

# The mirror brings the live Editor's compiled assemblies and process ids; the copy must compile on its own and must
# not kill the live Editor's IL post-processing runner.
Remove-Item (Join-Path $copy 'Library\ScriptAssemblies') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $copy 'Library\EditorInstance.json'), (Join-Path $copy 'Library\ilpp.pid') -Force -ErrorAction SilentlyContinue

# Isolated LOCALAPPDATA: avoids the live Editor's locked curl cache and log file.
$localAppData = "$copy-localappdata"
if (-not (Test-Path "$localAppData\Unity")) {
    New-Item -ItemType Directory -Force "$localAppData\Unity" | Out-Null
    foreach ($name in 'licenses', 'config') {
        $source = Join-Path $env:LOCALAPPDATA "Unity\$name"
        if (Test-Path $source) { Copy-Item $source "$localAppData\Unity\$name" -Recurse -Force }
    }
}
$env:LOCALAPPDATA = $localAppData

$unity = Join-Path $env:ProgramFiles 'Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
$log = Join-Path $copy 'devbuild.log'
$started = Get-Date
Write-Host "Building (batch mode, log: $log)"
$arguments = @('-batchmode', '-projectPath', $copy, '-buildTarget', 'Android',
    '-executeMethod', 'DevProbe.Editor.DevBuild.Run', '-logFile', $log)
if ($FromInit) { $arguments += '-cbjFromInit' }
$process = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList $arguments
Write-Host "Unity exit $($process.ExitCode) after $([int]((Get-Date) - $started).TotalSeconds) s"
Select-String -Path $log -Pattern 'DEVBUILD|error CS' | Select-Object -Last 20 | ForEach-Object { $_.Line }
$apk = Join-Path $copy 'Builds\cbj-dev.apk'
if ($process.ExitCode -ne 0 -or -not (Test-Path $apk)) { throw "Unity exited with $($process.ExitCode) or produced no APK; read $log." }
Write-Host "APK: $apk"
# robocopy's exit code (1-7 mean success) would otherwise leak out as the script's exit code.
exit 0
