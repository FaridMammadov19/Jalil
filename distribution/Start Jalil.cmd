@echo off
cd /d "%~dp0"
Jalil.exe

if errorlevel 1 (
  echo.
  echo Jalil stopped with an error.
  pause
)
