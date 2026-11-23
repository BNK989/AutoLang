# Builds the Linux .deb on Windows: publishes src/AutoLang.Linux for linux-x64 (self-contained, one file), then runs
# build-deb.sh inside WSL, which needs a Debian or Ubuntu distro with dpkg-deb (any stock Ubuntu has it).
#   .\packaging\linux\build-deb.ps1                          # 0.6.0~tester1, amd64
#   .\packaging\linux\build-deb.ps1 -Version 0.6.0~tester2
# Output: dist\autolang_<version>_amd64.deb
param(
    [string]$Version = '0.6.0~tester1',
    [string]$Distro = 'Ubuntu-24.04'
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$publish = Join-Path $root 'dist\linux-x64'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
& $dotnet publish (Join-Path $root 'src\AutoLang.Linux') -c Release -r linux-x64 --self-contained `
    -p:PublishSingleFile=true -p:DebugType=none -o $publish -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$wslRoot = (wsl -d $Distro -- wslpath -a ($root.Path -replace '\\', '/')).Trim()
wsl -d $Distro --cd $wslRoot -- bash packaging/linux/build-deb.sh dist/linux-x64 $Version amd64
if ($LASTEXITCODE -ne 0) { throw 'build-deb.sh failed' }
