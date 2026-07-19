@echo off
rem Rebuilds PCGuardian.exe from the included source code using the
rem C# compiler that ships inside Windows. No downloads needed.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File make-icon.ps1
%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:PCGuardian.exe /win32icon:guardian.ico /win32manifest:app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll PCGuardian.cs
if %errorlevel%==0 (echo Built PCGuardian.exe successfully.) else (echo Build failed.)
pause
