# PC Guardian

**Free, open-source PC health dashboard for Windows.** Scans for malware, audits for
keyloggers/botware/hijacks, cleans junk, and updates drivers safely — using tools already
built into Windows. No subscriptions. No scare tactics. Buy the Pro add-on once, own it forever.

> PC Guardian is a **system utility**, not an antivirus. Your real-time protection is Windows
> Defender (already on your PC, free). PC Guardian puts Windows' own security and maintenance in
> one place and adds audits most people don't know how to run — it doesn't replace Defender.

## What it does

- **Malware scans** — quick and full-disk, via Windows Defender.
- **Deep audit** — lists startup programs, scheduled tasks, services, live network connections,
  browser extensions, and checks for hosts/proxy/DNS hijacks and Defender tampering. Every line
  should be software you recognize.
- **Junk cleanup** — clears temp files. Never touches your documents.
- **Driver updates** — from Windows Update only (Microsoft-tested, signed), with a System Restore
  point created first so you can always roll back.

## Why open source?

Because you shouldn't have to trust a security tool — you should be able to *read* it. Every scan
and check is here in plain text. Don't trust the prebuilt `.exe`? Run `build.bat` and Windows'
own compiler rebuilds it from this source on your machine.

## Free vs Pro (open-core)

This repository is the **free core, open source under MIT** — all the manual tools above, forever.
**Pro** (one-time $5) adds convenience/automation — scheduled scans, one-click "Run Everything",
auto driver-update checks — as a closed add-on that funds development. We don't paywall your
safety; Pro just puts it on autopilot.

## Install

1. Download the latest release, unzip, keep the files together.
2. Double-click `PCGuardian.exe`, click **Yes** on the admin prompt.
3. First launch may show a SmartScreen "unknown publisher" warning (it's an unsigned homemade
   build, not malware) — click **More info → Run anyway**, or build it yourself with `build.bat`.

Requires Windows 10/11 with Windows Defender.

## Build from source

```bat
build.bat
```
Uses the C# compiler included with Windows (.NET Framework) — no downloads needed. Produces
`PCGuardian.exe` and `guardian.ico`.

## Files

| File | What |
|------|------|
| `PCGuardian.cs` | The window/app (C#) |
| `PCGuardian.ps1` | The engine — every scan and check |
| `app.manifest` | Requests admin + DPI awareness |
| `make-icon.ps1` | Draws the shield icon |
| `build.bat` | Rebuilds the exe from source |
| `Run PC Guardian.bat` | Terminal menu version |

## License

MIT — see [LICENSE](LICENSE). Built with Claude.

---

*Not affiliated with Microsoft, NortonLifeLock, or any antivirus vendor. "Windows" and "Windows
Defender" are trademarks of Microsoft Corporation.*
