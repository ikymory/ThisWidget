$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../packaging/Paths.ps1')
$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$fixture = Join-Path $temporary ('ThisWidgetPackaging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected $Expected; got $Actual" }
    Write-Host "PASS $Name"
}
try {
    $oldRoot = Join-Path $fixture 'LauncherCache/ThisWidget'
    $oldApp = Join-Path $oldRoot 'app'
    $download = Join-Path $fixture 'downloads/v1.0.1'
    $localData = Join-Path $fixture 'ordinary-AppData'
    New-Item -ItemType Directory -Path $oldApp, $download, $localData -Force | Out-Null
    $exe = Join-Path $oldApp 'ThisWidget.exe'
    [IO.File]::WriteAllText($exe, 'path fixture')
    [IO.File]::WriteAllText((Join-Path $download 'ThisWidget.exe'), 'new release fixture')
    $physicalRoot = [ThisWidgetInstallPaths]::Physical($oldRoot)
    Assert-Equal $physicalRoot (Select-ThisWidgetInstallRoot $download $localData @($exe)) 'Extracted update reuses registered install across launcher contexts'
    Assert-Equal $physicalRoot (Select-ThisWidgetInstallRoot $oldApp $localData @()) 'Installed installer and uninstaller retain their own physical root'
    Assert-Equal (Join-Path $localData 'ThisWidget') (Select-ThisWidgetInstallRoot $download $localData @((Join-Path $fixture 'missing.exe'))) 'Stale registration falls back to a fresh per-user installation'
    Assert-Equal (Join-Path $localData 'ThisWidget') (Select-ThisWidgetInstallRoot $download $localData @((Join-Path $download 'ThisWidget.exe'))) 'Portable registration cannot redirect installation into a release directory'
    Assert-Equal $physicalRoot (Select-ThisWidgetInstallRoot $download $localData @((Join-Path $fixture 'missing.exe'), $exe)) 'Valid shortcut target recovers a missing startup target'
    Assert-Equal ([ThisWidgetInstallPaths]::Physical($oldApp)) ([ThisWidgetInstallPaths]::Physical((Join-Path $oldApp '.'))) 'Physical source comparison avoids copying an installed folder onto itself'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ((Split-Path $resolved -Parent).TrimEnd('\') -ne $temporary -or (Split-Path $resolved -Leaf) -notlike 'ThisWidgetPackaging-*') {
        throw 'Unexpected fixture cleanup path.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
