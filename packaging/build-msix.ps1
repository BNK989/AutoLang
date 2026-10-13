<#
.SYNOPSIS
  Builds the AutoLang MSIX (PLAN.md §5, M4).

.DESCRIPTION
  Publishes the host self-contained for win-x64, stages it with the manifest and logos, builds resources.pri and packs
  dist\AutoLang-<version>-x64.msix.

  Store build (Partner Center signs it; upload the .msix as is):
    .\packaging\build-msix.ps1 -IdentityName 12345Publisher.AutoLang -Publisher "CN=XXXXXXXX-XXXX-..." -PublisherDisplayName "Your Name"
  The three values are on Partner Center > the app > Product identity.

  Sideload build for testing (the default): signed with the AutoLang test certificate from your certificate store.
    .\packaging\build-msix.ps1 -NewDevCertificate     # once: creates it, exports the public part
    .\packaging\build-msix.ps1                        # every build after that signs with it automatically
  Testers' PCs must trust dist\dev-signing.cer (Trusted People) to install the result; see packaging\STORE.md.
  Signed dev builds also produce dist\AutoLang-<version>-x64-testers.zip (msix + cer + INSTALL-testers.txt).
  A build is signed whenever a test certificate whose subject equals -Publisher is in Cert:\CurrentUser\My, so Store
  builds (Publisher = the Partner Center CN) stay unsigned. -Unsigned forces that; -CertificatePath signs with a .pfx.

  Needs the .NET 8 SDK. makeappx / makepri / signtool come from the Windows SDK when installed, otherwise from the
  Microsoft.Windows.SDK.BuildTools package on nuget.org (downloaded once into packaging\.tools).
#>
[CmdletBinding()]
param(
    [string]$IdentityName = "AutoLang.Dev",
    [string]$Publisher = "CN=AutoLang Dev",
    [string]$PublisherDisplayName = "AutoLang",
    [string]$Version,
    [string]$CertificatePath,
    [SecureString]$CertificatePassword,
    [switch]$NewDevCertificate,
    [switch]$Unsigned
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$hostProj = Join-Path $root "src\AutoLang.Host\AutoLang.Host.csproj"
$work = Join-Path $root "packaging\.build"
$layout = Join-Path $work "layout"
$dist = Join-Path $root "dist"
$devCertName = "AutoLang dev signing"
$devCer = Join-Path $root "packaging\dev-signing.cer"

function Find-SdkTool([string]$name) {
    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kits) {
        $hit = Get-ChildItem $kits -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object { [version]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\$name" } | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($hit) { return $hit }
    }
    $cache = Join-Path $root "packaging\.tools"
    $hit = Get-ChildItem $cache -Recurse -Filter $name -ErrorAction SilentlyContinue | Where-Object { $_.DirectoryName -match '\\x64$' } | Select-Object -First 1
    if ($hit) { return $hit.FullName }

    # Microsoft's redistributable of the same tools; latest stable 10.0.26100.x.
    Write-Host "Windows SDK not found; fetching Microsoft.Windows.SDK.BuildTools from nuget.org"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $base = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools"
    $ver = (Invoke-RestMethod "$base/index.json").versions | Where-Object { $_ -match '^10\.0\.26100\.\d+$' } |
        Sort-Object { [version]$_ } | Select-Object -Last 1
    if (-not $ver) { throw "No Microsoft.Windows.SDK.BuildTools 10.0.26100.x on nuget.org" }
    $dir = Join-Path $cache $ver
    New-Item -ItemType Directory -Force $dir | Out-Null
    $zip = Join-Path $cache "buildtools.$ver.zip"
    Invoke-WebRequest "$base/$ver/microsoft.windows.sdk.buildtools.$ver.nupkg" -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $dir -Force
    Remove-Item $zip
    $hit = Get-ChildItem $dir -Recurse -Filter $name | Where-Object { $_.DirectoryName -match '\\x64$' } | Select-Object -First 1
    if (-not $hit) { throw "$name not in Microsoft.Windows.SDK.BuildTools $ver" }
    $s = Get-AuthenticodeSignature $hit.FullName
    if ($s.Status -ne "Valid" -or $s.SignerCertificate.Subject -notmatch "O=Microsoft Corporation") {
        Remove-Item $dir -Recurse -Force
        throw "$name from the NuGet package is not Microsoft-signed ($($s.Status)); refusing to run it"
    }
    return $hit.FullName
}

function Invoke-Tool([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path -Leaf $exe) failed ($LASTEXITCODE)" }
}

if (-not $Version) {
    $v = ([xml](Get-Content $hostProj)).Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
    if (-not $v) { throw "No <Version> in $hostProj" }
    $Version = "$v.0"
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "Version must be a.b.c.0 (the Store reserves the 4th part), got $Version" }

function Find-DevCertificate {
    Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object {
        $_.FriendlyName -eq $devCertName -and $_.Subject -eq $Publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(1)
    } | Sort-Object NotAfter -Descending | Select-Object -First 1
}

if ($NewDevCertificate) {
    # A self-signed code-signing certificate for sideload tests only. The private key stays in this user's certificate
    # store, non-exportable (no .pfx, no password per build); only the public part is exported, for testers to trust.
    $cert = Find-DevCertificate
    if ($cert) {
        Write-Host "Test certificate already exists ($($cert.Thumbprint), valid until $($cert.NotAfter.ToString('yyyy-MM-dd'))); reusing it"
    } else {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName $devCertName `
            -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3) -KeyExportPolicy NonExportable `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        Write-Host "Test certificate created: $($cert.Subject), $($cert.Thumbprint), valid until $($cert.NotAfter.ToString('yyyy-MM-dd'))"
    }
    Export-Certificate -Cert $cert -FilePath $devCer | Out-Null
    Write-Host "Public part: $devCer"
}

$makeappx = Find-SdkTool "makeappx.exe"
$makepri = Find-SdkTool "makepri.exe"

# 1. Publish. Not single-file: the package is compressed anyway, and a plain folder starts faster and lets the
#    Store's block map deduplicate unchanged files between updates.
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null
dotnet publish $hostProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -o $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 2. Manifest and logos.
$manifest = Get-Content (Join-Path $PSScriptRoot "msix\AppxManifest.xml") -Raw
$esc = { param($s) [Security.SecurityElement]::Escape($s) }
$manifest = $manifest.Replace('$IdentityName$', (& $esc $IdentityName)).Replace('$Publisher$', (& $esc $Publisher)).
    Replace('$PublisherDisplayName$', (& $esc $PublisherDisplayName)).Replace('$Version$', $Version)
[IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object Text.UTF8Encoding $false))
Copy-Item (Join-Path $PSScriptRoot "msix\Assets") (Join-Path $layout "Assets") -Recurse

# 3. resources.pri: lets the shell pick the scale-/targetsize- variant of each logo. Indexes only the logos (a copy
#    with the same relative paths), and keeps every variant in this one file: the default config splits them out
#    into resources.scale-NNN.pri for resource packages, which a single .msix does not have.
$priConfig = Join-Path $work "priconfig.xml"
$priRoot = Join-Path $work "pri"
Copy-Item (Join-Path $PSScriptRoot "msix\Assets") (Join-Path $priRoot "Assets") -Recurse
Invoke-Tool $makepri @("createconfig", "/cf", $priConfig, "/dq", "en-US", "/pv", "10.0.0", "/o")
$cfg = [xml](Get-Content $priConfig -Raw)
$pack = $cfg.resources.SelectSingleNode("packaging")
if ($pack) { [void]$cfg.resources.RemoveChild($pack) }
$cfg.Save($priConfig)
Invoke-Tool $makepri @("new", "/pr", $priRoot, "/cf", $priConfig, "/mn", (Join-Path $layout "AppxManifest.xml"), "/of", (Join-Path $layout "resources.pri"), "/o")

# 4. Pack (validates the manifest against the schema).
New-Item -ItemType Directory -Force $dist | Out-Null
$msix = Join-Path $dist "AutoLang-$Version-x64.msix"
Invoke-Tool $makeappx @("pack", "/d", $layout, "/p", $msix, "/o")

# 5. Sign for sideloading. Store uploads stay unsigned: Partner Center signs them.
$devCert = if (-not $Unsigned -and -not $CertificatePath) { Find-DevCertificate }
if ($Unsigned -or (-not $CertificatePath -and -not $devCert)) {
    Write-Host "Unsigned (for Store upload): $msix"
    return
}
$signtool = Find-SdkTool "signtool.exe"
if ($CertificatePath) {
    $plain = if ($CertificatePassword) { [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertificatePassword)) } else { "" }
    $signArgs = @("sign", "/fd", "SHA256", "/f", $CertificatePath)
    if ($plain) { $signArgs += @("/p", $plain) }
} else {
    $signArgs = @("sign", "/fd", "SHA256", "/sha1", $devCert.Thumbprint, "/s", "My")
}
Invoke-Tool $signtool ($signArgs + @("/tr", "http://timestamp.digicert.com", "/td", "SHA256", $msix))
Write-Host "Signed: $msix"
if ($devCert) {
    # The public part goes next to the package, for testers to trust.
    $cer = Join-Path $dist "dev-signing.cer"
    Export-Certificate -Cert $devCert -FilePath $cer | Out-Null

    # One zip for new testers: the package, the certificate to trust, and the steps. The .msix is already
    # compressed, so the zip only stores it.
    $kit = Join-Path $dist "AutoLang-$Version-x64-testers.zip"
    $readme = Join-Path $work "How to install.txt"
    Copy-Item (Join-Path $PSScriptRoot "INSTALL-testers.txt") $readme
    Compress-Archive -Path $msix, $cer, $readme -DestinationPath $kit -CompressionLevel NoCompression -Force
    Write-Host "Testers trust first: $cer"
    Write-Host "Tester kit (msix + cer + steps): $kit"
}
