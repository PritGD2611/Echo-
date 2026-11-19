@echo off
rem Launcher for tools\unity-cli.ps1 (bypasses the machine's Restricted execution policy).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0unity-cli.ps1" %*
