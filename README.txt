PC GUARDIAN - free PC scanner & tune-up
========================================

What it is
----------
A free tool that does what the $200/year "PC cleaner" subscriptions sell:
  - Malware scans (quick + full disk) using Windows Defender
  - Deep audit for keyloggers, botware, browser hijacks & shady startup programs
  - Junk/temp file cleanup (never touches your documents)
  - Safe driver updates: Microsoft-tested drivers from Windows Update only,
    with a System Restore point created first so you can always undo

It only uses what's already inside Windows. Nothing is downloaded,
nothing phones home, there is no subscription because there is nothing to sell.

How to use it
-------------
1. Keep all the files in one folder.
2. Double-click PCGuardian.exe and click Yes on the admin prompt.
3. Pick a task from the left side. Results show up in the window.

First launch: Windows SmartScreen may warn "unknown publisher" - that's
because this is a homemade tool without a paid code-signing certificate,
not because anything is wrong. Click "More info" then "Run anyway".

Don't trust an exe someone sent you? Good instinct. Build it yourself:
------------------------------------------------------------------------
The complete source code is included:
  - PCGuardian.cs   the window/app (C#) - read it, it's short
  - PCGuardian.ps1  the engine (PowerShell) - every scan and check is visible
Run build.bat and Windows' own built-in C# compiler will produce a fresh
PCGuardian.exe from that source on your machine. Then you're running code
you can read, not a mystery binary.

Rule of thumb for the Deep Audit: every line it lists should be software
you recognize. Something unfamiliar? Ask Claude (or a techy friend) about
it before deleting anything.

Built with Claude, July 2026.
