@echo off
rem Rebuild from source (needs only the Windows built-in .NET Framework csc.exe)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
pause
