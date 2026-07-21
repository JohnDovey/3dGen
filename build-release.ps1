<#
.SYNOPSIS
    Builds a Release, self-contained single-file distribution of 3D Model Generator
    and zips it up for distribution.

.DESCRIPTION
    Publishes a single executable that bundles the app, its dependencies, the .NET
    runtime, native libraries (e.g. SQLite), and content files (Help docs/images).
    The target machine does not need the .NET Desktop Runtime installed.

    Tradeoff: the resulting .exe is larger than a framework-dependent multi-file
    publish, but distribution is one file and it runs out of the box.

.PARAMETER Runtime
    The .NET Runtime Identifier to publish for. Defaults to win-x64.

.PARAMETER SkipTests
    Skip running the test suite before publishing. By default the build stops
    if any test fails, so a broken build never gets zipped up for distribution.

.EXAMPLE
    .\build-release.ps1
    Runs the tests, then publishes and zips a win-x64 self-contained single-file release.

.EXAMPLE
    .\build-release.ps1 -SkipTests
    Same, without running tests first.
#>
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$uiProject = Join-Path $repoRoot "src\ModelGenerator.UI\ModelGenerator.UI.csproj"
$solution = Join-Path $repoRoot "ModelGenerator.slnx"
$distDir = Join-Path $repoRoot "dist"
$publishDir = Join-Path $distDir "publish"

[xml]$csprojXml = Get-Content $uiProject
$version = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = "0.0.0" }

Write-Host "3D Model Generator v$version - Release build ($Runtime, self-contained single-file)" -ForegroundColor Cyan
Write-Host "The .NET runtime and all app content are packed into one executable." -ForegroundColor Yellow
Write-Host ""

if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test $solution -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed - fix them before building a release, or pass -SkipTests to bypass."
    }
    Write-Host ""
}

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "Publishing single-file executable..." -ForegroundColor Cyan
dotnet publish $uiProject `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

# Single-file publish may still leave .pdb / .xml next to the exe; only ship the binary.
$exeName = "ModelGenerator.UI.exe"
$exePath = Join-Path $publishDir $exeName
if (-not (Test-Path $exePath)) {
    throw "Expected single-file output not found: $exePath"
}

$zipName = "ModelGenerator-v$version-$Runtime.zip"
$zipPath = Join-Path $distDir $zipName
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Zipping..." -ForegroundColor Cyan
Compress-Archive -Path $exePath -DestinationPath $zipPath

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Publish folder: $publishDir"
Write-Host "  Single-file exe: $exePath"
Write-Host "  Distributable:   $zipPath"
Write-Host ""
Write-Host "Note: self-contained - no separate .NET runtime install required." -ForegroundColor Yellow
