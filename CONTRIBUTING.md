# Contributing to PC Guardian

Thanks for wanting to help! PC Guardian is a free, open-source Windows health dashboard.
Contributions are welcome — bug fixes, new checks for the deep audit, better wording, and
Windows-version compatibility fixes especially.

## Ground rules

- **It's a system utility, not an antivirus.** Never describe it as antivirus or claim it
  "protects" the PC — the protection engine is Windows Defender. Keep docs and UI text honest.
- **Never weaken the safety guards.** The cleanup only touches temp files; driver installs create
  a System Restore point first. Don't remove those.
- **This repo is the free, open core.** The paid "Pro" automation and the license-signing tooling
  live outside this repo on purpose — please don't add license-key logic or paid-feature gates here.
- **No telemetry / phone-home.** The app must not report anything about the user's machine anywhere.

## Building

```bat
build.bat
```
Uses the C# compiler bundled with Windows (.NET Framework) — no downloads needed. Produces
`PCGuardian.exe` and `guardian.ico`.

## Testing a change

Run the engine directly without rebuilding the GUI:
```powershell
powershell -ExecutionPolicy Bypass -File PCGuardian.ps1 -Task audit
```
Valid tasks: `quick`, `full`, `audit`, `clean`, `drivers`, `driverinstall`, `definitions`, `all`.
Please test on Windows 10 and 11 if you can, and mention which you tested in your PR.

## Style

- Match the surrounding code — plain PowerShell / WinForms, no new dependencies.
- Keep audit output readable by non-technical users; explain findings in plain English.

## Reporting bugs

Open an issue with your Windows version, what you did, and what happened. Screenshots help.
Security issues: see [SECURITY.md](SECURITY.md) — please don't open a public issue for those.
