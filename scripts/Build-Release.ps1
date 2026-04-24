<#
.SYNOPSIS
    Builds and packages the Arbor.HttpClient.Desktop release artifacts.

.DESCRIPTION
    Performs all packaging steps shared by both the release workflow (release.yml)
    and the CI release-verification job (ci.yml):
      1. Create MSIX package layout (logo assets + versioned AppxManifest)
      2. Pack the MSIX with makeappx.exe
      3. Generate the SPDX SBOM with sbom-tool
      4. Assert the SBOM manifest exists at the expected path
      5. Create a self-signed code-signing certificate
      6. Sign the MSIX with signtool.exe
      7. Generate SHA-256 checksums for the MSIX and certificate

    The release workflow then attests build provenance and creates the GitHub release.
    The CI verification job stops after step 7 (no publishing).

.PARAMETER Version
    4-part version string to embed in AppxManifest (e.g. "1.0.42.0").

.PARAMETER PublishDir
    Directory containing the dotnet publish output. Defaults to "publish/win-x64".

.PARAMETER MsixPath
    Output path for the signed MSIX package. Defaults to "Arbor.HttpClient.Desktop.msix".

.PARAMETER CertPath
    Output path for the exported sideloading certificate (.cer). Defaults to
    "Arbor.HttpClient.Desktop.cer".

.PARAMETER PfxPath
    Output path for the signing PFX. Defaults to "packaging.pfx".

.EXAMPLE
    .\scripts\Build-Release.ps1 -Version "1.0.42.0"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version,

    [string]$PublishDir = "publish/win-x64",
    [string]$MsixPath   = "Arbor.HttpClient.Desktop.msix",
    [string]$CertPath   = "Arbor.HttpClient.Desktop.cer",
    [string]$PfxPath    = "packaging.pfx"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 1. Create MSIX package layout
# ---------------------------------------------------------------------------
Write-Host "==> Creating MSIX package layout (version: $Version)"

$assetsDir = "$PublishDir/Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$manifest = Get-Content "src/Arbor.HttpClient.Desktop/packaging/AppxManifest.xml" -Raw
$manifest  = $manifest -replace 'VERSION_PLACEHOLDER', $Version
$manifest | Set-Content "$PublishDir/AppxManifest.xml" -Encoding UTF8

$color = "#0078D4"
magick -size  44x44  xc:"$color" "$assetsDir/Square44x44Logo.png"
magick -size 150x150 xc:"$color" "$assetsDir/Square150x150Logo.png"
magick -size 310x150 xc:"$color" "$assetsDir/Wide310x150Logo.png"
magick -size  50x50  xc:"$color" "$assetsDir/StoreLogo.png"
magick -size 620x300 xc:"$color" "$assetsDir/SplashScreen.png"

Write-Host "MSIX package layout created."

# ---------------------------------------------------------------------------
# 2. Pack MSIX
# ---------------------------------------------------------------------------
Write-Host "==> Packing MSIX"

$makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "makeappx.exe" |
    Where-Object { $_.FullName -like "*x64*" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
Write-Host "makeappx: $makeappx"
& $makeappx pack /d $PublishDir /p $MsixPath
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------------
# 3. Generate SBOM
# ---------------------------------------------------------------------------
Write-Host "==> Generating SBOM"

# sbom-tool always appends /_manifest to the -m directory, so pass $PublishDir
# directly so the manifest lands at $PublishDir/_manifest/spdx_2.2/manifest.spdx.json
sbom-tool Generate `
    -b $PublishDir `
    -bc . `
    -pn "Arbor.HttpClient.Desktop" `
    -pv $Version `
    -ps "Niklas Lundberg" `
    -m $PublishDir `
    -V Information

# ---------------------------------------------------------------------------
# 4. Verify SBOM manifest exists
# ---------------------------------------------------------------------------
$sbomPath = "$PublishDir/_manifest/spdx_2.2/manifest.spdx.json"
if (-not (Test-Path $sbomPath)) {
    Write-Error "SBOM manifest not found at expected path: $sbomPath"
    exit 1
}
Write-Host "SBOM manifest verified at: $sbomPath"

# ---------------------------------------------------------------------------
# 5. Create self-signed code-signing certificate
# ---------------------------------------------------------------------------
Write-Host "==> Creating self-signed signing certificate"

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=Arbor.HttpClient" `
    -KeyUsage DigitalSignature `
    -FriendlyName "Arbor.HttpClient" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pwd = ConvertTo-SecureString -String "CI_SIGN_CERT" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $PfxPath   -Password $pwd | Out-Null
Export-Certificate    -Cert $cert -FilePath $CertPath              | Out-Null
Write-Host "Certificate thumbprint: $($cert.Thumbprint)"

# ---------------------------------------------------------------------------
# 6. Sign MSIX
# ---------------------------------------------------------------------------
Write-Host "==> Signing MSIX"

$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" |
    Where-Object { $_.FullName -like "*x64*" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
Write-Host "signtool: $signtool"
& $signtool sign /fd SHA256 /a /f $PfxPath /p "CI_SIGN_CERT" $MsixPath
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------------
# 7. Generate artifact checksums
# ---------------------------------------------------------------------------
Write-Host "==> Generating checksums"

Get-FileHash $MsixPath -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash.ToLowerInvariant())  $MsixPath"
} | Set-Content "$MsixPath.sha256" -Encoding UTF8

Get-FileHash $CertPath -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash.ToLowerInvariant())  $CertPath"
} | Set-Content "$CertPath.sha256" -Encoding UTF8

Write-Host "Release build artifacts ready."
Write-Host "  MSIX:     $MsixPath"
Write-Host "  Cert:     $CertPath"
Write-Host "  SBOM:     $sbomPath"
