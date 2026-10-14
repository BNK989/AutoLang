# Renders the Microsoft Store screenshots, 1366x768, one set per UI language:
#   screenshots\<code>\screenshot-1.png .. screenshot-4.png, and captions.txt (one caption per line, in upload order)
# Step 1 draws AutoLang's own tray menu and Languages window in each language (ui\<code>-*.png, tools\AutoLang.Screens);
# step 2 lays them into screenshots.html with the words from screenshot-strings.js, in headless Edge. Nothing appears on screen.
# Usage: render-screenshots.ps1 [-Languages he,ru]   (default: every language in screenshot-strings.js)
param([string[]]$Languages)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here "..\..")).Path

if (-not $Languages) {
    $Languages = [regex]::Matches((Get-Content (Join-Path $here "screenshot-strings.js") -Raw -Encoding UTF8), '(?m)^  ([a-z]{2}): \{') |
        ForEach-Object { $_.Groups[1].Value }
}

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge not found" }
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "$env:ProgramFiles\dotnet\dotnet.exe" }

# Build into a scratch folder: a running AutoLang.exe locks the DLLs in src\AutoLang.Host\bin.
$bin = Join-Path $env:TEMP "autolang-screens\bin\"
& $dotnet build (Join-Path $root "tools\AutoLang.Screens") -c Release --nologo -v q "-p:BaseOutputPath=$bin"
if ($LASTEXITCODE -ne 0) { throw "AutoLang.Screens did not build" }
$tool = Join-Path $bin "Release\net8.0-windows\AutoLang.Screens.exe"

$uiDir = Join-Path $here "ui"
New-Item -ItemType Directory -Force $uiDir | Out-Null
$profileDir = Join-Path $env:TEMP "autolang-screens-edge"
$page = ([Uri](Join-Path $here "screenshots.html")).AbsoluteUri

$strings = Get-Content (Join-Path $here "screenshot-strings.js") -Raw -Encoding UTF8

foreach ($lang in $Languages) {
    & $tool $lang $uiDir
    if ($LASTEXITCODE -ne 0) { throw "AutoLang.Screens failed for $lang" }
    $outDir = Join-Path $here "screenshots\$lang"
    New-Item -ItemType Directory -Force $outDir | Out-Null
    # The slide headlines are the captions: the line starting "h: [" inside this language's block.
    $block = [regex]::Match($strings, "(?ms)^  ${lang}: \{.*?^  \}").Value
    $h = [regex]::Match($block, "(?m)^\s+h: \[(.*)\],?\s*$").Groups[1].Value
    $captions = [regex]::Matches($h, "'((?:[^'\\]|\\.)*)'") | ForEach-Object { $_.Groups[1].Value -replace '\\(.)', '$1' }
    if (@($captions).Count -ne 4) { throw "Expected 4 captions for $lang, found $(@($captions).Count)" }
    [IO.File]::WriteAllLines((Join-Path $outDir "captions.txt"), [string[]]$captions, (New-Object Text.UTF8Encoding $true))
    foreach ($n in 1..4) {
        $out = Join-Path $outDir "screenshot-$n.png"
        if (Test-Path $out) { Remove-Item $out }
        # Start-Process: Edge reports progress on stderr, which Windows PowerShell would turn into a terminating error.
        $edgeArgs = "--headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --user-data-dir=`"$profileDir`" " +
            "--window-size=1366,768 --screenshot=`"$out`" `"$page`?lang=$lang#$n`""
        Start-Process -FilePath $edge -ArgumentList $edgeArgs -Wait -WindowStyle Hidden
        if (-not (Test-Path $out)) { throw "Edge did not write $out" }
        "wrote $out"
    }
}
