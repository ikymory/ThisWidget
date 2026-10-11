[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = Join-Path $repo 'artifacts'
$architecture = $Runtime.Substring(4)
$name = "ThisWidget-Windows-$architecture"
$folder = [IO.Path]::GetFullPath((Join-Path $artifacts $name))
$zip = Join-Path $artifacts "$name.zip"
if ((Split-Path $folder -Parent) -ne $artifacts -or (Split-Path $folder -Leaf) -ne $name) {
    throw 'Unexpected publish directory.'
}
if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
New-Item -ItemType Directory -Path $folder -Force | Out-Null
& $Dotnet publish (Join-Path $repo 'src/ThisWidget') -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $folder
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
foreach ($file in @('Install.cmd', 'Install.ps1', 'Uninstall.cmd', 'Uninstall.ps1')) {
    Copy-Item -LiteralPath (Join-Path $repo "packaging/$file") -Destination $folder
}
foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CONTRIBUTING.md', 'RELEASE_NOTES.md')) {
    Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $folder
}
Copy-Item -LiteralPath (Join-Path $repo 'packaging/licenses') -Destination $folder -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'docs') -Destination $folder -Recurse
Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $zip -Force -CompressionLevel Optimal
$stream = [IO.File]::OpenRead($zip)
$hash = [Security.Cryptography.SHA256]::Create()
try { $checksum = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
finally { $hash.Dispose(); $stream.Dispose() }
[IO.File]::WriteAllText("$zip.sha256", "$checksum  $name.zip`n", [Text.UTF8Encoding]::new($false))
Write-Host "Release: $zip"
Write-Host "SHA-256: $checksum"
