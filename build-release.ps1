<#
.SYNOPSIS
    Builds a Release, framework-dependent distribution of 3D Model Generator and
    zips it up for distribution.

.DESCRIPTION
    This is a framework-dependent publish: the .NET 10 Desktop Runtime is NOT
    bundled into the output. Anyone running the published app must already have
    the .NET 10 Desktop Runtime installed (https://dotnet.microsoft.com/download).
    This keeps the distributed zip small; the tradeoff is that runtime-less
    machines need to install it once before the app will launch.

.PARAMETER Runtime
    The .NET Runtime Identifier to publish for. Defaults to win-x64.

.PARAMETER SkipTests
    Skip running the test suite before publishing. By default the build stops
    if any test fails, so a broken build never gets zipped up for distribution.

.EXAMPLE
    .\build-release.ps1
    Runs the tests, then publishes and zips a win-x64 framework-dependent release.

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

Write-Host "3D Model Generator v$version - Release build ($Runtime, framework-dependent)" -ForegroundColor Cyan
Write-Host "The .NET 10 Desktop Runtime is NOT bundled - it must already be installed on the target machine." -ForegroundColor Yellow
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

Write-Host "Publishing..." -ForegroundColor Cyan
dotnet publish $uiProject `
    -c Release `
    -r $Runtime `
    --self-contained false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$zipName = "ModelGenerator-v$version-$Runtime.zip"
$zipPath = Join-Path $distDir $zipName
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Zipping..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Publish folder: $publishDir"
Write-Host "  Distributable:  $zipPath"
Write-Host ""
Write-Host "Note: requires the .NET 10 Desktop Runtime on any machine that runs this build." -ForegroundColor Yellow
