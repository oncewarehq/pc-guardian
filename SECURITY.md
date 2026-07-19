# Security Policy

PC Guardian runs with administrator rights and touches security-sensitive parts of Windows, so we
take reports seriously.

## Reporting a vulnerability

Please **do not** open a public issue for security problems. Email **oncewarehq@gmail.com** with:

- What the issue is and where (file / feature)
- Steps to reproduce
- What an attacker could do with it

We'll acknowledge as soon as we can and keep you posted on the fix. Responsible disclosure is
appreciated — give us a reasonable window to patch before publishing details.

## Scope

In scope: anything in this repository (the free PC Guardian app and engine).

Out of scope: Windows Defender itself and the Windows Update service (report those to Microsoft),
and the separate paid/licensing components, which aren't part of this repo.

## For users

PC Guardian is a **system utility**, not antivirus — your real-time protection is Windows Defender.
The app never sends information about your machine anywhere; every check runs locally. Don't trust
the prebuilt `.exe`? Rebuild it from source with `build.bat`.
