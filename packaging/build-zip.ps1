<#
.SYNOPSIS
  Builds the portable tester zip: dist\AutoLang-win-x64.zip (one self-contained exe, the word lists, README.txt).

.DESCRIPTION
  The no-install alternative to the MSIX. Unsigned, so SmartScreen warns on first run (README.txt says how to get
  past it). Start-at-login uses the HKCU Run key from the tray menu.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\AutoLang-win-x64"
$zip = Join-Path $root "dist\AutoLang-win-x64.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\AutoLang.Host\AutoLang.Host.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root "src\AutoLang.Host\README-testers.md") (Join-Path $out "README.txt")
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
Write-Host "$zip ($([math]::Round((Get-Item $zip).Length / 1MB)) MB)"
