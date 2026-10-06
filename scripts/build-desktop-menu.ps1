param(
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$Compiler,
    [string]$SdkBin,
    [string]$CertificatePath,
    [switch]$Prototype
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not $Compiler) { $Compiler = Join-Path $taskRoot '.local/desktop-menu-tools/llvm-mingw-20260922-ucrt-x86_64/bin/clang++.exe' }
if (-not $SdkBin) { $SdkBin = Join-Path $taskRoot '.local/desktop-menu-tools/windows-sdk/bin/10.0.28000.0/x64' }
if (-not $CertificatePath) { $CertificatePath = Join-Path $taskRoot '.local/desktop-menu-signing/MyFences.pfx' }
foreach ($tool in @($Compiler, (Join-Path $SdkBin 'makeappx.exe'), (Join-Path $SdkBin 'signtool.exe'))) {
    if (-not (Test-Path -LiteralPath $tool)) { throw "Required build tool missing: $tool. Pass -Compiler / -SdkBin for your installed toolchain." }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dll = Join-Path $output 'MyFences.ShellExtension.dll'
& $Compiler --target=x86_64-w64-windows-gnu -shared -static -std=c++17 -O2 (Join-Path $taskRoot 'src/MyFences.ShellExtension/DesktopCommand.cpp') (Join-Path $taskRoot 'src/MyFences.ShellExtension/exports.def') -o $dll -lole32 -lshlwapi -lshell32 -ladvapi32 -luuid
if ($LASTEXITCODE -ne 0) { throw 'Native shell command compilation failed.' }
$certificateDirectory = Split-Path -Parent $CertificatePath
New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
$password = ConvertTo-SecureString 'local-development-only' -AsPlainText -Force
if (-not (Test-Path -LiteralPath $CertificatePath)) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=MyFences Local Development' -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(2)
    Export-PfxCertificate -Cert $cert -FilePath $CertificatePath -Password $password | Out-Null
}
$cert = Get-PfxCertificate -FilePath $CertificatePath -Password $password
Export-Certificate -Cert $cert -FilePath (Join-Path $output 'MyFences.DesktopMenu.cer') | Out-Null
$stage = Join-Path $output 'identity-build'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$manifest = [xml](Get-Content -LiteralPath (Join-Path $taskRoot 'src/MyFences.ShellExtension/AppxManifest.xml') -Raw)
$appVersion = ([xml](Get-Content -LiteralPath (Join-Path $taskRoot 'src/MyFences.App/MyFences.App.csproj') -Raw)).Project.PropertyGroup.Version
$manifest.Package.Identity.Version = $appVersion + '.0'
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path (Join-Path $stage 'Assets'),(Join-Path $output 'Assets') -Force | Out-Null
$icon = [System.Drawing.Icon]::new((Join-Path $taskRoot 'src/MyFences.App/Assets/MyFences.ico'))
$bitmap = $icon.ToBitmap()
try {
    foreach ($name in @('StoreLogo.png','Square150x150Logo.png','Square44x44Logo.png')) {
        $bitmap.Save((Join-Path $stage ('Assets/' + $name)), [System.Drawing.Imaging.ImageFormat]::Png)
        Copy-Item -LiteralPath (Join-Path $stage ('Assets/' + $name)) -Destination (Join-Path $output 'Assets') -Force
    }
} finally { $bitmap.Dispose(); $icon.Dispose() }
$package = Join-Path $output 'MyFences.DesktopMenu.msix'
& (Join-Path $SdkBin 'makeappx.exe') pack /o /d $stage /nv /p $package
if ($LASTEXITCODE -ne 0) { throw 'Identity package build failed.' }
& (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /f $CertificatePath /p 'local-development-only' $package
if ($LASTEXITCODE -ne 0) { throw 'Identity package signing failed.' }
if ($Prototype) { Set-Content -LiteralPath (Join-Path $output 'prototype-mode') -Value 'Invoke only writes prototype.log; never changes desktop state.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'scripts/trust-desktop-menu-certificate.ps1') -Destination $output -Force
$toolLicense = Join-Path (Split-Path -Parent (Split-Path -Parent $Compiler)) 'LICENSE.TXT'
if (Test-Path -LiteralPath $toolLicense) {
    New-Item -ItemType Directory -Path (Join-Path $output 'licenses/llvm-mingw') -Force | Out-Null
    Copy-Item -LiteralPath $toolLicense -Destination (Join-Path $output 'licenses/llvm-mingw/LICENSE.TXT') -Force
}
Get-Item -LiteralPath $dll,$package,(Join-Path $output 'MyFences.DesktopMenu.cer') | Select-Object Name,Length
