param(
  [string]$Version = "1.0.0",
  [string]$Runtime = "win-x64",
  [string]$Configuration = "Release",
  [string]$OutputName = "PulseCheck.Agent.Service.Setup-local",
  [string]$IsccPath = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputRoot = Join-Path $root "artifacts\service-local-$stamp"
$trayPublishDir = Join-Path $outputRoot "tray"
$servicePublishDir = Join-Path $outputRoot "service"
$installerOutputDir = Join-Path $outputRoot "installer"
$trayProject = Join-Path $root "PulseCheck.Agent\PulseCheck.Agent.csproj"
$serviceProject = Join-Path $root "PulseCheck.Agent.Service\PulseCheck.Agent.Service.csproj"
$innoScript = Join-Path $root "installer\PulseCheck.Agent.iss"

New-Item -ItemType Directory -Force -Path $trayPublishDir, $servicePublishDir, $installerOutputDir | Out-Null

dotnet restore $trayProject
if ($LASTEXITCODE -ne 0) { throw "dotnet restore tray failed with code $LASTEXITCODE" }

dotnet restore $serviceProject
if ($LASTEXITCODE -ne 0) { throw "dotnet restore service failed with code $LASTEXITCODE" }

dotnet publish $trayProject -c $Configuration -r $Runtime --self-contained true -o $trayPublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish tray failed with code $LASTEXITCODE" }

dotnet publish $serviceProject -c $Configuration -r $Runtime --self-contained true -o $servicePublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish service failed with code $LASTEXITCODE" }

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
  $candidatePaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
  )

  $IsccPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or !(Test-Path $IsccPath)) {
  throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -IsccPath."
}

& $IsccPath `
  "/DMyAppVersion=$Version" `
  "/DMyTrayPublishedDir=$trayPublishDir" `
  "/DMyServicePublishedDir=$servicePublishDir" `
  "/DMyOutputDir=$installerOutputDir" `
  "/DMyOutputBaseFilename=$OutputName" `
  $innoScript
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with code $LASTEXITCODE" }

$setup = Join-Path $installerOutputDir "$OutputName.exe"
if (!(Test-Path $setup)) {
  throw "Installer was not generated at $setup"
}

$hash = Get-FileHash -Algorithm SHA256 $setup
$hash.Hash | Set-Content -Path "$setup.sha256" -Encoding ASCII

Write-Host "Installer: $setup"
Write-Host "SHA256: $($hash.Hash)"
