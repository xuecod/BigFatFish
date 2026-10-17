# build.ps1 - build the Big Fat Fish desktop pet with the Windows built-in C# compiler.
# NOTE: intentionally ASCII-only. Windows PowerShell 5.1 reads .ps1 as ANSI without a BOM,
# so non-ASCII literals here would break the script.

$ErrorActionPreference = 'Stop'

$root   = $PSScriptRoot
$src    = Join-Path $root 'src'
$outDir = Join-Path $root 'bin'
$csc    = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) {
    throw "C# compiler not found: $csc (needs the built-in .NET Framework 4.x)"
}
if (-not (Test-Path $src)) {
    throw "Source folder not found: $src"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @(Get-ChildItem -Path $src -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No .cs files in $src" }

$targets = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$outDir\BigFatFish.exe",
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Management.dll'
)

$icon = Join-Path $root 'assets\fish.ico'

# Render the tray/EXE icon with the art module when it is missing.
if (-not (Test-Path $icon)) {
    $iconTool = Join-Path $root 'tools\MakeIcon.cs'
    if (Test-Path $iconTool) {
        Write-Host "Generating assets\fish.ico ..." -ForegroundColor Cyan
        $iconExe = Join-Path $root 'tools\_iconbuild.exe'
        & $csc /nologo /target:exe "/out:$iconExe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll `
            (Join-Path $src 'Contracts.cs') (Join-Path $src 'Art.cs') $iconTool | Out-Null
        if ($LASTEXITCODE -eq 0 -and (Test-Path $iconExe)) {
            & $iconExe $icon | Out-Null
            Remove-Item $iconExe -Force -ErrorAction SilentlyContinue
        } else {
            Write-Host "Icon generation failed, skipping (not required)" -ForegroundColor Yellow
        }
    }
}

if (Test-Path $icon) { $targets += "/win32icon:$icon" }

Write-Host ("Compiling {0} source files -> {1}\BigFatFish.exe" -f $sources.Count, $outDir) -ForegroundColor Cyan
& $csc ($targets + $sources)
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

# A copy with a Chinese file name, assembled from code points to keep this script ASCII-only:
# U+5927 U+80A5 U+9C7C U+684C U+5BA0 = "da fei yu zhuo chong"
$cnName = [string]::Join('', [char]0x5927, [char]0x80A5, [char]0x9C7C, [char]0x684C, [char]0x5BA0) + '.exe'
$cn = Join-Path $outDir $cnName
Copy-Item -Path (Join-Path $outDir 'BigFatFish.exe') -Destination $cn -Force

# Copy assets (sprites, icon) next to the exe so "bin" works stand-alone.
$assetsSrc = Join-Path $root 'assets'
if (Test-Path $assetsSrc) {
    Copy-Item -Path $assetsSrc -Destination $outDir -Recurse -Force
}

Write-Host "Build OK:" -ForegroundColor Green
Write-Host "  $outDir\BigFatFish.exe"
Write-Host "  $cn"
