# Personal/development builds only. Run explicitly as administrator once if you trust this MyFences build.
param([string]$Certificate = (Join-Path $PSScriptRoot 'MyFences.DesktopMenu.cer'))
$ErrorActionPreference = 'Stop'
$cert = Get-PfxCertificate -FilePath $Certificate
if ($cert.Subject -ne 'CN=MyFences Local Development') { throw 'Unexpected MyFences certificate subject.' }
Write-Host ('Trusting this MyFences personal signing certificate on this computer: ' + $cert.Thumbprint)
Import-Certificate -FilePath $Certificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
