# Exercise the published executable with an isolated journal and real single-instance messages.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$exe = [IO.Path]::GetFullPath($Executable)
$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$fixture = Join-Path $temporary ('ThisWidgetLaunch-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$journal = Join-Path $fixture 'journal.json'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupBefore = if (Test-Path -LiteralPath $runKey) { (Get-Item -LiteralPath $runKey).GetValue('ThisWidget') } else { $null }
$process = $null
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ThisWidgetLaunchCheck {
    delegate bool Callback(IntPtr h, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    public static bool Visible(uint process) {
        bool visible = false;
        Callback visit = (h, data) => {
            uint owner; GetWindowThreadProcessId(h, out owner);
            if (owner == process && IsWindowVisible(h)) visible = true;
            return true;
        };
        EnumWindows((h, data) => { visit(h, data); EnumChildWindows(h, visit, data); return true; }, IntPtr.Zero);
        return visible;
    }
}
'@
function Assert-True([bool]$Value, [string]$Name) {
    if (-not $Value) { throw $Name }
    Write-Host "PASS $Name"
}
function Send-Control([string]$Operation) {
    $control = Start-Process -FilePath $exe -ArgumentList @('--data', ('"' + $journal + '"'), $Operation) -WindowStyle Hidden -PassThru
    # PowerShell launches GUI executables asynchronously with &, leaving LASTEXITCODE stale.
    $null = $control.Handle
    if (-not $control.WaitForExit(10000)) { throw "Control command timed out: $Operation" }
    if ($control.ExitCode -ne 0) { throw "Control command failed: $Operation ($($control.ExitCode))" }
}
try {
    [IO.File]::WriteAllText($journal, '{"Schema":1,"Settings":{"WidgetEnabled":false,"DesktopMode":true},"Weekly":{"Tuesday":["Back","Chest"]},"Days":{"2026-01-01":{"Tasks":["Gym"],"Checked":["Gym"],"Completed":true,"Notes":"preserved"}}}')
    $process = Start-Process -FilePath $exe -ArgumentList @('--data', ('"' + $journal + '"'), '--background') -WindowStyle Hidden -PassThru
    Assert-True ($process.WaitForInputIdle(10000)) 'Published app starts with an isolated profile'
    Assert-True (-not [ThisWidgetLaunchCheck]::Visible([uint32]$process.Id)) 'Background launch respects removed widget visibility'
    Send-Control --background
    Assert-True (-not [ThisWidgetLaunchCheck]::Visible([uint32]$process.Id)) 'Repeated startup command does not revive a removed widget'
    Send-Control --home
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $saved = [IO.File]::ReadAllText($journal) | ConvertFrom-Json
        if ($saved.Settings.WidgetEnabled) { break }
        [Threading.Thread]::Sleep(100)
    } while ([DateTime]::UtcNow -lt $deadline)
    Assert-True ($saved.Settings.WidgetEnabled -and $saved.Settings.DesktopMode -and $saved.Settings.LockPosition -and -not $saved.Settings.AlwaysOnTop) 'Home command adds a fixed locked widget through the existing instance'
    Assert-True ($saved.Days.'2026-01-01'.Completed -and $saved.Days.'2026-01-01'.Notes -eq 'preserved' -and ($saved.Weekly.Tuesday -join ',') -eq 'Back,Chest') 'Home command preserves recorded workouts and weekly plans'
    $startupAfter = if (Test-Path -LiteralPath $runKey) { (Get-Item -LiteralPath $runKey).GetValue('ThisWidget') } else { $null }
    Assert-True ($startupBefore -eq $startupAfter) 'Isolated profile cannot change Windows startup registration'
    Send-Control --exit
    Assert-True ($process.WaitForExit(10000)) 'Exit command stops the isolated instance gracefully'
} finally {
    if ($process -and -not $process.HasExited) {
        Send-Control --exit
        if (-not $process.WaitForExit(10000)) { throw 'Isolated test instance did not exit; fixture retained.' }
    }
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ((Split-Path $resolved -Parent).TrimEnd('\') -ne $temporary -or (Split-Path $resolved -Leaf) -notlike 'ThisWidgetLaunch-*') { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
