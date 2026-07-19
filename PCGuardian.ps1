<#
    PC GUARDIAN - free PC scanner & maintenance tool
    Built with Claude. Uses only what is already inside Windows:
      - Windows Defender for malware scanning (it IS a full antivirus)
      - Windows Update for driver updates (Microsoft-tested, WHQL-signed only)
      - PowerShell for the deep audit and junk cleanup
    No downloads, no subscriptions, nothing phones home.

    Run "Run PC Guardian.bat" for the menu, or call directly:
      powershell -ExecutionPolicy Bypass -File PCGuardian.ps1 -Task audit
#>
param(
    [ValidateSet('quick','full','audit','clean','drivers','driverinstall','definitions','all')]
    [string]$Task,
    [switch]$AutoYes   # used by the PC Guardian app, which confirms via its own dialog
)

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal $id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Menu mode wants admin so scans, cleanup and driver installs all work.
if (-not $Task -and -not (Test-Admin)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$PSCommandPath`""
    exit
}

function Write-Section($text) {
    Write-Host ""
    Write-Host ("=" * 62) -ForegroundColor DarkCyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host ("=" * 62) -ForegroundColor DarkCyan
}
function Write-Ok($text)   { Write-Host "  [OK]    $text" -ForegroundColor Green }
function Write-Warn($text) { Write-Host "  [CHECK] $text" -ForegroundColor Yellow }
function Write-Info($text) { Write-Host "          $text" -ForegroundColor Gray }

# ---------------------------------------------------------------- scans

function Show-ThreatReport($since) {
    $threats = @(Get-MpThreatDetection -ErrorAction SilentlyContinue |
                 Where-Object { $_.InitialDetectionTime -gt $since })
    if ($threats.Count -eq 0) {
        Write-Ok "No threats found."
    } else {
        foreach ($t in $threats) {
            $info = Get-MpThreat -ThreatID $t.ThreatID -ErrorAction SilentlyContinue
            Write-Warn "$($info.ThreatName)  ->  $($t.Resources -join '; ')"
        }
        Write-Info "Defender quarantines threats automatically."
        Write-Info "Review them in: Windows Security > Virus & threat protection > Protection history"
    }
}

function Invoke-QuickScan {
    Write-Section "DEFENDER QUICK SCAN  (common malware locations, a few minutes)"
    $start = Get-Date
    Start-MpScan -ScanType QuickScan
    Show-ThreatReport $start
}

function Invoke-FullScan {
    Write-Section "DEFENDER FULL SCAN  (every file on disk, 30-90 minutes)"
    Write-Info "The PC stays usable while it runs. Go play something."
    $start = Get-Date
    Start-MpScan -ScanType FullScan
    Show-ThreatReport $start
}

function Invoke-Definitions {
    Write-Section "UPDATE VIRUS DEFINITIONS"
    try {
        Update-MpSignature -ErrorAction Stop
        Write-Ok "Definitions updated: $((Get-MpComputerStatus).AntivirusSignatureLastUpdated)"
    } catch {
        Write-Warn "Could not update (no internet?): $($_.Exception.Message)"
    }
}

# ---------------------------------------------------------------- deep audit

function Invoke-DeepAudit {
    Write-Section "DEEP AUDIT - persistence, botware, hijacks, trackers"
    Write-Info "Rule of thumb: every line should be software you recognize."
    Write-Info "Anything unfamiliar? Don't delete blindly - ask Claude about it first."

    Write-Host ""
    Write-Host "  --- Programs that start with Windows ---" -ForegroundColor White
    $runKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
    )
    foreach ($key in $runKeys) {
        $props = Get-ItemProperty $key -ErrorAction SilentlyContinue
        if ($props) {
            $props.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object {
                Write-Info "$($_.Name)  =  $($_.Value)"
            }
        }
    }
    Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup",
                  "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Info "Startup folder: $($_.Name)" }

    Write-Host ""
    Write-Host "  --- Non-Microsoft scheduled tasks ---" -ForegroundColor White
    Get-ScheduledTask -ErrorAction SilentlyContinue |
        Where-Object { $_.TaskPath -notlike '\Microsoft\*' -and $_.State -ne 'Disabled' } |
        ForEach-Object {
            $exe = ($_.Actions | ForEach-Object { $_.Execute }) -join ' '
            Write-Info "$($_.TaskName)  ->  $exe"
        }

    Write-Host ""
    Write-Host "  --- Services running outside standard folders ---" -ForegroundColor White
    $odd = Get-CimInstance Win32_Service | Where-Object {
        $_.PathName -and $_.PathName -notmatch 'system32|SysWOW64|Program Files|Microsoft.Net|Windows Defender|servicing'
    }
    if ($odd) { $odd | ForEach-Object { Write-Warn "$($_.Name) [$($_.State)]  ->  $($_.PathName)" } }
    else      { Write-Ok "All services run from standard Windows/Program Files locations." }

    Write-Host ""
    Write-Host "  --- Processes running from AppData/ProgramData/Temp ---" -ForegroundColor White
    Get-Process | Where-Object { $_.Path -and $_.Path -match 'AppData|ProgramData|\\Temp\\|Users\\Public' } |
        Select-Object -Unique Name, Path | ForEach-Object { Write-Info "$($_.Name)  ->  $($_.Path)" }

    Write-Host ""
    Write-Host "  --- Who is talking to the internet right now ---" -ForegroundColor White
    Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue |
        Where-Object { $_.RemoteAddress -notmatch '^(127\.|::1|fe80|10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[01])\.)' } |
        ForEach-Object {
            $p = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
            [PSCustomObject]@{ Name = $p.Name; Remote = "$($_.RemoteAddress):$($_.RemotePort)" }
        } | Group-Object Name | Sort-Object Count -Descending | ForEach-Object {
            Write-Info "$($_.Name): $($_.Count) connection(s)"
        }

    Write-Host ""
    Write-Host "  --- Traffic hijack checks ---" -ForegroundColor White
    $hosts = Get-Content C:\Windows\System32\drivers\etc\hosts -ErrorAction SilentlyContinue |
             Where-Object { $_ -notmatch '^\s*#' -and $_.Trim() }
    if ($hosts) { $hosts | ForEach-Object { Write-Warn "hosts file entry: $_" } }
    else        { Write-Ok "hosts file is clean." }

    $proxy = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
    if ($proxy.ProxyEnable -eq 1) { Write-Warn "A proxy is enabled: $($proxy.ProxyServer) - spyware sometimes does this." }
    else                          { Write-Ok "No proxy configured." }
    if ($proxy.AutoConfigURL)     { Write-Warn "Auto-config proxy URL set: $($proxy.AutoConfigURL)" }

    $wmi = @(Get-CimInstance -Namespace root\subscription -ClassName __EventConsumer -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -ne 'SCM Event Log Consumer' })
    if ($wmi.Count -gt 0) { $wmi | ForEach-Object { Write-Warn "WMI persistence found: $($_.Name) - worth investigating." } }
    else                  { Write-Ok "No hidden WMI persistence." }

    Write-Host ""
    Write-Host "  --- Browser extensions ---" -ForegroundColor White
    $extRoots = @(
        @{ Browser = 'Opera GX'; Path = "$env:APPDATA\Opera Software\Opera GX Stable\Extensions" },
        @{ Browser = 'Edge';     Path = "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Extensions" },
        @{ Browser = 'Chrome';   Path = "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Extensions" }
    )
    foreach ($root in $extRoots) {
        if (Test-Path $root.Path) {
            Get-ChildItem $root.Path -Directory | ForEach-Object {
                $manifest = Get-ChildItem $_.FullName -Recurse -Filter manifest.json -ErrorAction SilentlyContinue |
                            Select-Object -First 1
                if ($manifest) {
                    $m = Get-Content $manifest.FullName -Raw | ConvertFrom-Json
                    $name = $m.name
                    if ($name -like '__MSG_*') {
                        $key = $name -replace '__MSG_|__', ''
                        $loc = Get-ChildItem (Join-Path (Split-Path $manifest.FullName) '_locales\en*') -Filter messages.json -Recurse -ErrorAction SilentlyContinue |
                               Select-Object -First 1
                        if ($loc) {
                            $msgs = Get-Content $loc.FullName -Raw | ConvertFrom-Json
                            try { $name = $msgs.$key.message } catch { }
                        }
                    }
                    Write-Info "$($root.Browser): $name"
                }
            }
        }
    }

    Write-Host ""
    Write-Host "  --- Is anything tampering with Defender? ---" -ForegroundColor White
    $p = Get-MpPreference
    if ($p.DisableRealtimeMonitoring) { Write-Warn "Real-time protection is OFF - turn it back on in Windows Security!" }
    else                              { Write-Ok "Real-time protection on." }
    if ($p.DisableBehaviorMonitoring) { Write-Warn "Behavior monitoring is OFF." } else { Write-Ok "Behavior monitoring on." }
    if ($p.MAPSReporting -eq 0)       { Write-Warn "Cloud protection is OFF." }      else { Write-Ok "Cloud protection on." }
    if ($p.PUAProtection -ne 1)       { Write-Warn "PUA/adware blocking is OFF." }   else { Write-Ok "PUA/adware blocking on." }
}

# ---------------------------------------------------------------- cleanup

function Invoke-Cleanup {
    Write-Section "JUNK CLEANUP  (temp files only - never your documents)"
    $targets = @($env:TEMP, 'C:\Windows\Temp')
    $freedTotal = 0
    foreach ($dir in $targets) {
        if (-not (Test-Path $dir)) { continue }
        $before = (Get-ChildItem $dir -Recurse -Force -ErrorAction SilentlyContinue |
                   Measure-Object Length -Sum).Sum
        Get-ChildItem $dir -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try { Remove-Item $_.FullName -Recurse -Force -Confirm:$false -ErrorAction Stop } catch { }
        }
        $after = (Get-ChildItem $dir -Recurse -Force -ErrorAction SilentlyContinue |
                  Measure-Object Length -Sum).Sum
        $freed = [math]::Max(0, ($before - $after))
        $freedTotal += $freed
        Write-Ok ("{0}: freed {1:N0} MB (files in use are kept)" -f $dir, ($freed / 1MB))
    }
    Write-Ok ("Total freed: {0:N0} MB" -f ($freedTotal / 1MB))
    $drive = Get-PSDrive C
    Write-Info ("C: drive now has {0:N1} GB free." -f ($drive.Free / 1GB))
}

# ---------------------------------------------------------------- drivers

function Get-DriverUpdates {
    Write-Info "Asking Windows Update for driver updates (this can take 1-3 minutes)..."
    $session  = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    try {
        $result = $searcher.Search("IsInstalled=0 and Type='Driver'")
    } catch {
        Write-Warn "Could not reach Windows Update: $($_.Exception.Message)"
        return $null
    }
    return @{ Session = $session; Result = $result }
}

function Invoke-DriverCheck {
    Write-Section "DRIVER UPDATE CHECK  (Windows Update - Microsoft-signed only)"
    $wu = Get-DriverUpdates
    if (-not $wu) { return }
    if ($wu.Result.Updates.Count -eq 0) {
        Write-Ok "No driver updates pending - everything Windows manages is current."
        Write-Info "GPU drivers: keep using the NVIDIA App. Laptop firmware: Lenovo Vantage."
    } else {
        $i = 1
        foreach ($u in $wu.Result.Updates) { Write-Warn "$i. $($u.Title)"; $i++ }
        Write-Info "Run option 6 (or -Task driverinstall) to install them safely."
    }
}

function Invoke-DriverInstall {
    Write-Section "SAFE DRIVER INSTALL"
    if (-not (Test-Admin)) {
        Write-Warn "Installing drivers needs Administrator. Launch via 'Run PC Guardian.bat'."
        return
    }
    $wu = Get-DriverUpdates
    if (-not $wu) { return }
    if ($wu.Result.Updates.Count -eq 0) { Write-Ok "No driver updates to install."; return }

    $i = 1
    foreach ($u in $wu.Result.Updates) { Write-Warn "$i. $($u.Title)"; $i++ }
    if ($AutoYes) { $go = 'y' } else { $go = Read-Host "  Install ALL of the above? (y/n)" }
    if ($go -ne 'y') { Write-Info "Nothing installed."; return }

    Write-Info "Creating a System Restore point first - your undo button..."
    try {
        Checkpoint-Computer -Description "PC Guardian - before driver updates" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
        Write-Ok "Restore point created."
    } catch {
        # Windows only allows one restore point per 24h by default; that earlier point still protects you.
        Write-Warn "Restore point not created: $($_.Exception.Message)"
        if ($AutoYes) { $go = 'y'; Write-Info "(An earlier restore point may still cover you.)" } else { $go = Read-Host "  Continue anyway? (y/n)" }
        if ($go -ne 'y') { return }
    }

    $coll = New-Object -ComObject Microsoft.Update.UpdateColl
    foreach ($u in $wu.Result.Updates) {
        if (-not $u.EulaAccepted) { $u.AcceptEula() }
        [void]$coll.Add($u)
    }
    Write-Info "Downloading..."
    $dl = $wu.Session.CreateUpdateDownloader()
    $dl.Updates = $coll
    [void]$dl.Download()
    Write-Info "Installing..."
    $inst = $wu.Session.CreateUpdateInstaller()
    $inst.Updates = $coll
    $res = $inst.Install()
    if ($res.ResultCode -eq 2) { Write-Ok "All drivers installed successfully." }
    else                       { Write-Warn "Finished with result code $($res.ResultCode) (2 = success, 3 = success but reboot pending)." }
    if ($res.RebootRequired)   { Write-Warn "Restart the PC to finish applying drivers." }
}

# ---------------------------------------------------------------- menu / dispatch

function Invoke-Everything {
    Invoke-Definitions
    Invoke-QuickScan
    Invoke-DeepAudit
    Invoke-Cleanup
    Invoke-DriverCheck
    Write-Section "ALL DONE"
    Write-Info "For a full-disk scan run option 2 - it takes a while, so it's not part of 'everything'."
}

function Show-Menu {
    while ($true) {
        Write-Host ""
        Write-Host "  +------------------------------------------------+" -ForegroundColor Cyan
        Write-Host "  |   PC GUARDIAN - your free scanner & tune-up    |" -ForegroundColor Cyan
        Write-Host "  +------------------------------------------------+" -ForegroundColor Cyan
        Write-Host "   1. Quick malware scan            (~5 min)"
        Write-Host "   2. Full-disk malware scan        (30-90 min)"
        Write-Host "   3. Deep audit: keyloggers, botware, hijacks"
        Write-Host "   4. Clean junk / temp files"
        Write-Host "   5. Check for driver updates"
        Write-Host "   6. Install driver updates safely (restore point first)"
        Write-Host "   7. Update virus definitions"
        Write-Host "   8. Run everything (1 + 3 + 4 + 5 + 7)"
        Write-Host "   Q. Quit"
        $choice = Read-Host "  Pick an option"
        switch ($choice.ToUpper()) {
            '1' { Invoke-QuickScan }
            '2' { Invoke-FullScan }
            '3' { Invoke-DeepAudit }
            '4' { Invoke-Cleanup }
            '5' { Invoke-DriverCheck }
            '6' { Invoke-DriverInstall }
            '7' { Invoke-Definitions }
            '8' { Invoke-Everything }
            'Q' { return }
            default { Write-Warn "Not an option." }
        }
    }
}

switch ($Task) {
    'quick'         { Invoke-QuickScan }
    'full'          { Invoke-FullScan }
    'audit'         { Invoke-DeepAudit }
    'clean'         { Invoke-Cleanup }
    'drivers'       { Invoke-DriverCheck }
    'driverinstall' { Invoke-DriverInstall }
    'definitions'   { Invoke-Definitions }
    'all'           { Invoke-Everything }
    default         { Show-Menu }
}
