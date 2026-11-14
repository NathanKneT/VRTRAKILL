@echo off
rem Removes VRTRAKILL from the ULTRAKILL game folder (asks first). It puts back the game's original globalgamemanagers
rem and removes BepInEx only when no other mod uses it. The steps are in VRTRAKILL-install.ps1 next to this file.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VRTRAKILL-install.ps1" -Uninstall %*
