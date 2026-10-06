param([string]$Dotnet, [string]$OutputDirectory, [string]$NativeCompiler, [string]$WindowsSdkBin, [string]$SigningCertificate)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not $Dotnet) {
    $localSdk = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
}
$project = Join-Path $taskRoot 'src\MyFences.App\MyFences.App.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('.local\publish-v' + $version) }
$output = Join-Path $OutputDirectory ('MyFences-' + $version + '-win-x64')
& $Dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$assets = Get-Content -LiteralPath (Join-Path $taskRoot 'src\MyFences.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$downloads = $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies
foreach ($runtimeName in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    $dependency = $downloads | Where-Object { $_.name -eq $runtimeName } | Select-Object -First 1
    $runtimeVersion = $dependency.version.Trim('[',']').Split(',')[0].Trim()
    $runtimeFolder = $assets.packageFolders.PSObject.Properties.Name |
        ForEach-Object { Join-Path $_ ($runtimeName.ToLowerInvariant() + '\' + $runtimeVersion) } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $runtimeFolder) { throw "Runtime package not found: $runtimeName" }
    $licenseOutput = Join-Path $output ('licenses\' + $runtimeName)
    New-Item -ItemType Directory -Path $licenseOutput -Force | Out-Null
    Get-ChildItem -LiteralPath $runtimeFolder -File |
        Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' } |
        Copy-Item -Destination $licenseOutput -Force
}
foreach ($name in @('README.md','README.en.md','LICENSE','THIRD_PARTY.md')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $name) -Destination $output -Force
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination $output -Recurse -Force
if (Test-Path -LiteralPath (Join-Path $taskRoot 'src\MyFences.ShellExtension\DesktopCommand.cpp')) {
    $menuBuild = Join-Path $OutputDirectory 'desktop-menu-build'
    $menuOptions = @{ OutputDirectory=$menuBuild }
    if ($NativeCompiler) { $menuOptions.Compiler=$NativeCompiler }
    if ($WindowsSdkBin) { $menuOptions.SdkBin=$WindowsSdkBin }
    if ($SigningCertificate) { $menuOptions.CertificatePath=$SigningCertificate }
    & (Join-Path $taskRoot 'scripts\build-desktop-menu.ps1') @menuOptions
    foreach ($name in @('MyFences.ShellExtension.dll','MyFences.DesktopMenu.msix','MyFences.DesktopMenu.cer','trust-desktop-menu-certificate.ps1','Assets')) {
        Copy-Item -LiteralPath (Join-Path $menuBuild $name) -Destination $output -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $menuBuild 'licenses\llvm-mingw') -Destination (Join-Path $output 'licenses') -Recurse -Force
}
$zip = Join-Path $OutputDirectory ('MyFences-' + $version + '-win-x64.zip')
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force

Add-Type -AssemblyName System.IO.Compression
$sourceZip = Join-Path $OutputDirectory ('MyFences-' + $version + '-source.zip')
$archiveFile = [System.IO.File]::Open($sourceZip, [System.IO.FileMode]::Create)
$archive = [System.IO.Compression.ZipArchive]::new($archiveFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $sourceFiles = @()
    foreach ($folder in @('src','tests','scripts','docs')) {
        $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $taskRoot $folder) -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
    foreach ($name in @('.gitignore','AGENTS.md','Directory.Build.props','MyFences.slnx','README.md','README.en.md','LICENSE','THIRD_PARTY.md')) {
        $sourceFiles += Get-Item -LiteralPath (Join-Path $taskRoot $name)
    }
    foreach ($file in $sourceFiles) {
        $entry = $file.FullName.Substring($taskRoot.Length + 1).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $archiveFile.Dispose() }
Write-Output $zip
Write-Output $sourceZip
