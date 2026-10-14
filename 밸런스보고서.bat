@echo off
rem Balance report: PlayLogs -> PlayLogs\report.html, then open it in the browser
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\balance-report.ps1"
if errorlevel 1 (
  echo.
  pause
  exit /b 1
)
start "" "%~dp0PlayLogs\report.html"
