@echo off
rem Installs VRTRAKILL into the ULTRAKILL game folder: finds the game through Steam, copies the mod, keeps your settings.
rem The steps are in VRTRAKILL-install.ps1 next to this file (plain text, open it to read what it does).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VRTRAKILL-install.ps1" %*
