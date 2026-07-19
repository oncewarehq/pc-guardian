@echo off
rem PC Guardian launcher - double-click me.
rem The script will ask for Administrator (click Yes) so scans,
rem cleanup and driver installs all work.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PCGuardian.ps1"
