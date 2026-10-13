<#
.SYNOPSIS
  Builds the portable exe: dist\AutoLang.exe (self-contained, word lists inside, nothing else needed).

.DESCRIPTION
  The no-install alternative to the MSIX. Unsigned, so SmartScreen warns on first run (the site and
  src\AutoLang.Host\README-testers.md say how to get past it). Start-at-login uses the HKCU Run key from the tray menu.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "packaging\.build\portable"
$exe = Join-Path $root "dist\AutoLang.exe"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\AutoLang.Host\AutoLang.Host.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$files = @(Get-ChildItem $out -Recurse -File)
if ($files.Count -ne 1 -or $files[0].Name -ne "AutoLang.exe") {
    throw "expected only AutoLang.exe in the publish output, got: $($files.Name -join ', ')"
}
New-Item -ItemType Directory -Force (Split-Path $exe) | Out-Null
Copy-Item $files[0].FullName $exe -Force
Write-Host "$exe ($([math]::Round((Get-Item $exe).Length / 1MB)) MB)"
