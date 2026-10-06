param([string]$Dotnet, [string]$NativeCompiler, [string]$WindowsSdkBin, [string]$SigningCertificate)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not $Dotnet) {
    $sdk = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $sdk) { $sdk } else { 'dotnet' }
}
$commit = (& git -C $taskRoot rev-parse HEAD)
if ($LASTEXITCODE -ne 0) { throw 'A committed HEAD is required.' }
$projectXml = & git -C $taskRoot show ($commit + ':src/MyFences.App/MyFences.App.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the committed version.' }
$version = ([xml]($projectXml -join [Environment]::NewLine)).Project.PropertyGroup.Version
$tag = 'v' + $version
$releaseRoot = Join-Path $taskRoot 'release'
$output = Join-Path $releaseRoot $tag
New-Item -ItemType Directory -Path $output -Force | Out-Null
$manifestPath = Join-Path $output 'release.json'
function Write-Manifest($record) {
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($manifestPath + '.tmp') -Encoding utf8
    Move-Item -LiteralPath ($manifestPath + '.tmp') -Destination $manifestPath -Force
}
if (Test-Path -LiteralPath $manifestPath) {
    $previous = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($previous.status -eq 'complete') {
        if ($previous.commit -ne $commit) { throw 'This version already belongs to another commit; update the version.' }
        foreach ($file in $previous.files) {
            if ((Get-FileHash -LiteralPath (Join-Path $output $file.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw 'Existing delivery checksum mismatch.' }
        }
        Write-Output $manifestPath
        return
    }
}
$record = [ordered]@{ version=$version; tag=$tag; commit=$commit; created_at=[DateTime]::UtcNow.ToString('o'); completed_at=$null; status='pending'; files=@(); error=$null }
Write-Manifest $record
try {
    $buildRoot = Join-Path $taskRoot ('.local\release-build-' + $tag + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    $exportZip = Join-Path $buildRoot 'commit.zip'
    & git -C $taskRoot archive --format=zip ('--output=' + $exportZip) $commit .gitignore AGENTS.md Directory.Build.props MyFences.slnx README.md README.en.md LICENSE THIRD_PARTY.md src scripts tests docs
    if ($LASTEXITCODE -ne 0) { throw 'Commit export failed.' }
    $sourceRoot = Join-Path $buildRoot 'source'
    Expand-Archive -LiteralPath $exportZip -DestinationPath $sourceRoot
    if (-not $NativeCompiler) { $NativeCompiler = Join-Path $taskRoot '.local\desktop-menu-tools\llvm-mingw-20260922-ucrt-x86_64\bin\clang++.exe' }
    if (-not $WindowsSdkBin) { $WindowsSdkBin = Join-Path $taskRoot '.local\desktop-menu-tools\windows-sdk\bin\10.0.28000.0\x64' }
    if (-not $SigningCertificate) { $SigningCertificate = Join-Path $taskRoot '.local\desktop-menu-signing\MyFences.pfx' }
    & (Join-Path $sourceRoot 'scripts\publish.ps1') -Dotnet $Dotnet -OutputDirectory $output -NativeCompiler $NativeCompiler -WindowsSdkBin $WindowsSdkBin -SigningCertificate $SigningCertificate
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $portable = Join-Path $output ('MyFences-' + $version + '-win-x64.zip')
    $source = Join-Path $output ('MyFences-' + $version + '-source.zip')
    $archive = [IO.Compression.ZipFile]::OpenRead($portable)
    try {
        $exe = $archive.Entries | Where-Object FullName -eq 'MyFences.exe'
        if (-not $exe -or $exe.Length -eq 0) { throw 'Portable archive lacks MyFences.exe.' }
    } finally { $archive.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($source)
    try {
        $expected = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse | Where-Object FullName -notmatch '[\\/](bin|obj)[\\/]' | ForEach-Object { $_.FullName.Substring($sourceRoot.Length + 1).Replace('\','/') })
        $actual = @($archive.Entries | ForEach-Object FullName)
        if (Compare-Object $expected $actual) { throw 'Source archive file list differs from the commit export.' }
        foreach ($entry in $archive.Entries) {
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $entryHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($entryHash -ne (Get-FileHash -LiteralPath (Join-Path $sourceRoot $entry.FullName) -Algorithm SHA256).Hash) { throw ('Source mismatch: ' + $entry.FullName) }
        }
    } finally { $archive.Dispose() }
    $notes = Get-Content -LiteralPath (Join-Path $sourceRoot 'docs\CHANGELOG.md') -Raw
    $section = [regex]::Match($notes, '(?ms)^## ' + [regex]::Escape($version) + '\r?\n.*?(?=^## |\z)')
    if (-not $section.Success) { throw ('Missing committed release notes for ' + $version) }
    @(
        ('# MyFences ' + $tag), '', ('Commit: ' + $commit), '',
        $section.Value.Trim(), '',
        'Delivery verification: both ZIPs opened successfully; source entries match the clean commit export. SHA-256 checksums are recorded in release.json.'
    ) | Set-Content -LiteralPath (Join-Path $output 'CHANGELOG.md') -Encoding utf8
    $record.files = @(('MyFences-' + $version + '-win-x64.zip'), ('MyFences-' + $version + '-source.zip'), 'CHANGELOG.md') | ForEach-Object {
        @{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $output $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $record.status = 'complete'
    $record.completed_at = [DateTime]::UtcNow.ToString('o')
    Write-Manifest $record
    $completed = @(Get-ChildItem -LiteralPath $releaseRoot -Directory | ForEach-Object {
        $path = Join-Path $_.FullName 'release.json'
        if (Test-Path -LiteralPath $path) {
            $item = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if ($item.status -eq 'complete' -and $item.tag -eq $_.Name) { [pscustomobject]@{ path=$_.FullName; completed_at=$item.completed_at } }
        }
    } | Sort-Object completed_at -Descending)
    foreach ($old in ($completed | Select-Object -Skip 3)) {
        $target = [IO.Path]::GetFullPath($old.path)
        $prefix = [IO.Path]::GetFullPath($releaseRoot) + [IO.Path]::DirectorySeparatorChar
        if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target is outside release.' }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Write-Output $manifestPath
} catch {
    $record.status = 'pending'
    $record.completed_at = $null
    $record.error = $_.Exception.Message
    Write-Manifest $record
    throw
}
