# Renders the Microsoft Store screenshots (screenshot-1.png .. screenshot-4.png, 1366x768) from screenshots.html with
# headless Edge. Nothing appears on screen. ui-*.png are AutoLang's own windows drawn off-screen; redraw them by hand
# when the Languages window or the tray menu changes.
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge not found" }
$profileDir = Join-Path $env:TEMP "autolang-screens-edge"
$page = ([Uri](Join-Path $here "screenshots.html")).AbsoluteUri

foreach ($n in 1..4) {
    $out = Join-Path $here "screenshot-$n.png"
    if (Test-Path $out) { Remove-Item $out }
    # Start-Process: Edge reports progress on stderr, which Windows PowerShell would turn into a terminating error.
    $edgeArgs = "--headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --user-data-dir=`"$profileDir`" " +
        "--window-size=1366,768 --screenshot=`"$out`" `"$page#$n`""
    Start-Process -FilePath $edge -ArgumentList $edgeArgs -Wait -WindowStyle Hidden
    if (-not (Test-Path $out)) { throw "Edge did not write $out" }
    "wrote $out"
}
