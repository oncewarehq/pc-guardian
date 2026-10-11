<#
    Regression tests for PC Guardian. Run from the repo root:
      powershell -NoProfile -ExecutionPolicy Bypass -File tests\Test-PCGuardian.ps1 [-Slow]

    Needs no admin. The engine tests point APPDATA/LOCALAPPDATA at fake browser profiles;
    the app tests build a copy of the GUI (without the admin manifest) next to a stub
    engine script, then drive it with UI Automation.
    -Slow also runs a real Defender quick scan (~1 min) to check the progress heartbeat.
#>
param([switch]$Slow)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$work = Join-Path $env:TEMP "pcguardian-tests"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null

$script:failures = 0
function Assert($cond, $name, $detail) {
    if ($cond) { Write-Host "  PASS  $name" -ForegroundColor Green }
    else       { Write-Host "  FAIL  $name" -ForegroundColor Red; if ($detail) { Write-Host "        $detail" -ForegroundColor DarkGray }; $script:failures++ }
}

# Runs the engine as a child process with a fake profile; returns stdout lines and stderr text.
function Invoke-Engine($task, $appData, $localAppData) {
    $psi = New-Object Diagnostics.ProcessStartInfo 'powershell.exe'
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$repo\PCGuardian.ps1`" -Task $task -AutoYes"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.EnvironmentVariables['APPDATA'] = $appData
    $psi.EnvironmentVariables['LOCALAPPDATA'] = $localAppData
    $p = [Diagnostics.Process]::Start($psi)
    $errTask = $p.StandardError.ReadToEndAsync()
    $out = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    @{ Out = ($out -split "`r?`n"); Err = $errTask.Result }
}

function New-Extension($root, $id, $manifestJson, $locales) {
    $dir = Join-Path $root "$id\1.0_0"
    New-Item -ItemType Directory $dir -Force | Out-Null
    Set-Content (Join-Path $dir 'manifest.json') $manifestJson -Encoding UTF8
    foreach ($lang in $locales.Keys) {
        New-Item -ItemType Directory (Join-Path $dir "_locales\$lang") -Force | Out-Null
        Set-Content (Join-Path $dir "_locales\$lang\messages.json") $locales[$lang] -Encoding UTF8
    }
}

# Every "--- Section ---" header must be followed by at least one line before the next header.
function Get-EmptySections($lines) {
    $empty = @(); $current = $null; $count = 0
    foreach ($l in $lines) {
        if ($l -match '^\s*--- (.+) ---\s*$') {
            if ($current -and $count -eq 0) { $empty += $current }
            $current = $Matches[1]; $count = 0
        } elseif ($current -and $l.Trim()) { $count++ }
    }
    if ($current -and $count -eq 0) { $empty += $current }
    $empty
}

Write-Host "`nEngine: deep audit" -ForegroundColor Cyan

# Profile A: no browsers, empty startup folder -> every section must still say something (M8)
$emptyRoaming = Join-Path $work 'empty\Roaming'; $emptyLocal = Join-Path $work 'empty\Local'
New-Item -ItemType Directory "$emptyRoaming\Microsoft\Windows\Start Menu\Programs\Startup", $emptyLocal -Force | Out-Null
$r = Invoke-Engine audit $emptyRoaming $emptyLocal
$empty = @(Get-EmptySections $r.Out)
Assert ($empty.Count -eq 0) "M8: no audit section prints nothing" ("silent sections: " + ($empty -join ', '))

# Profile B: broken manifest + non-English-only extension + normal extension (M9, L13)
$roaming = Join-Path $work 'exts\Roaming'; $local = Join-Path $work 'exts\Local'
$opera = "$roaming\Opera Software\Opera GX Stable\Extensions"
New-Item -ItemType Directory $local -Force | Out-Null
New-Extension $opera 'aaa' '{ "name": "Broken Ext", ' @{}
New-Extension $opera 'bbb' '{ "name": "__MSG_appName__", "default_locale": "de" }' @{ de = '{ "appName": { "message": "Deutscher Name" } }' }
New-Extension $opera 'ccc' '{ "name": "Plain Extension" }' @{}
$r = Invoke-Engine audit $roaming $local
$text = $r.Out -join "`n"
Assert ($r.Err -notmatch 'ConvertFrom-Json') "M9: broken manifest causes no PowerShell error" (($r.Err -split "`n" | Select-Object -First 1) -join '')
Assert ($text -match 'Opera GX: .*unreadable') "M9: broken manifest is reported as unreadable"
Assert ($text -notmatch 'Opera GX:\s*$' -and $text -notmatch "Opera GX:\s*`n") "M9: no blank extension names"
Assert ($text -match 'Opera GX: Deutscher Name') "L13: name resolved from default_locale"
Assert ($text -match 'Opera GX: Plain Extension') "normal extension still listed"

Write-Host "`nApp (GUI) with a stub engine" -ForegroundColor Cyan

# Build the real app source without the admin manifest, next to a stub engine.
$appDir = Join-Path $work 'app'
New-Item -ItemType Directory $appDir | Out-Null
$exe = Join-Path $appDir 'PCGuardianTest.exe'
& "$env:windir\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$repo\PCGuardian.cs" | Out-Host
Assert (Test-Path $exe) "app builds"
@'
param([string]$Task, [switch]$AutoYes)
Write-Host "  [OK]    stub $Task started"
[Console]::Error.WriteLine("stub error line")
Start-Sleep -Seconds 30
'@ | Set-Content (Join-Path $appDir 'PCGuardian.ps1')

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [Windows.Automation.AutomationElement]
function Get-AppWindow($proc) {
    for ($i = 0; $i -lt 50; $i++) {
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne 0) { return $AE::FromHandle($proc.MainWindowHandle) }
        Start-Sleep -Milliseconds 100
    }
    throw "app window never appeared"
}
# Flat WinForms buttons surface as plain panes, so find controls by name and click with BM_CLICK.
Add-Type -Namespace Native -Name User32 -MemberDefinition '[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);'
function Find-Button($win, $prefix) {
    foreach ($b in $win.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
        if ($b.Current.Name.StartsWith($prefix)) { return $b }
    }
    throw "control '$prefix' not found"
}
function Click($btn) { [Native.User32]::PostMessage([IntPtr]$btn.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
function Get-ConsoleText($win) {
    ($win.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) |
        ForEach-Object { $_.Current.Name }) -join "`n"
}
$StopLabel = [string][char]0x2715   # the Stop button's label starts with this mark

# M11: stderr lines are labelled as errors, not as ordinary [CHECK] warnings
$p = Start-Process $exe -PassThru
$win = Get-AppWindow $p
Click (Find-Button $win 'Deep Audit')
Start-Sleep -Seconds 4
$consoleText = Get-ConsoleText $win
Assert ($consoleText -match '\[ERROR\]\s*stub error line') "M11: engine errors are marked [ERROR]" "console: $($consoleText -replace '\s+',' ')"

# Stale-exit race: Stop, then immediately start another task -> app must stay busy
Click (Find-Button $win $StopLabel)
Click (Find-Button $win 'Clean Junk')
Start-Sleep -Seconds 2
$audit = Find-Button $win 'Deep Audit'; $stop = Find-Button $win $StopLabel
Assert ((-not $audit.Current.IsEnabled) -and $stop.Current.IsEnabled) "race: stopped task's exit doesn't un-busy the new task"
$p.CloseMainWindow() | Out-Null; $p.WaitForExit(10000) | Out-Null
if (-not $p.HasExited) { $p.Kill() }

# M12: closing the window while a task runs must never crash (repeat to catch the race)
$crashes = 0; $runs = 10
for ($i = 1; $i -le $runs; $i++) {
    $p = Start-Process $exe -PassThru
    $win = Get-AppWindow $p
    Click (Find-Button $win 'Deep Audit')
    Start-Sleep -Milliseconds (300 + 150 * $i)
    $p.CloseMainWindow() | Out-Null
    if (-not $p.WaitForExit(10000)) { $p.Kill(); $crashes++ }   # hung = WER crash dialog
    elseif ($p.ExitCode -ne 0) { $crashes++ }
}
Get-Process powershell -ErrorAction SilentlyContinue | Where-Object { $_.Path -and (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine -like "*$appDir*" } | Stop-Process -Force
Assert ($crashes -eq 0) "M12: closing mid-task never crashes ($runs runs)" "$crashes of $runs runs crashed or hung"

if ($Slow) {
    Write-Host "`nEngine: real Defender quick scan (slow)" -ForegroundColor Cyan
    $r = Invoke-Engine quick $env:APPDATA $env:LOCALAPPDATA
    Assert (($r.Out -match 'still scanning').Count -ge 1) "M7: scan prints a progress heartbeat"
}

Write-Host ""
if ($script:failures) { Write-Host "$($script:failures) test(s) failed." -ForegroundColor Red; exit 1 }
Write-Host "All tests passed." -ForegroundColor Green
