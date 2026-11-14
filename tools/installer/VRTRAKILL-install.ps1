# Installs VRTRAKILL from the extracted release zip into the ULTRAKILL game folder. Run by "INSTALL VRTRAKILL.bat"
# (double-click), which sits next to this script at the zip root. Works with the Windows PowerShell 5.1 every Windows has.
#
# What it does:
#  1. Finds ULTRAKILL through Steam: the Steam folder from the registry, then every library in
#     steamapps/libraryfolders.vdf, looking for steamapps/common/ULTRAKILL/ULTRAKILL.exe. If that fails, it asks for
#     the folder. -GameDir skips the search.
#  2. Waits for ULTRAKILL to be closed.
#  3. Copies every file of the zip into the game folder, except these two installer files. An existing
#     VRTRAKILL_Config.json is kept (the mod adds new settings to it by itself).
# It changes nothing outside the game folder and downloads nothing. -FindOnly only reports the folder it would use.
param(
    [string]$GameDir,
    [switch]$FindOnly,
    [switch]$NoPause
)
$ErrorActionPreference = 'Stop'
$Source = $PSScriptRoot
$Installer = @('INSTALL VRTRAKILL.bat', 'VRTRAKILL-install.ps1')
# never overwritten when the game folder already has them: the player's settings
$KeepIfPresent = @('BepInEx\plugins\VRTRAKILL\VRTRAKILL_Config.json')

function Say([string]$Text, [string]$Color = 'Gray') { Write-Host $Text -ForegroundColor $Color }

function Get-SteamLibraries {
    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        $item = Get-ItemProperty $key -ErrorAction SilentlyContinue
        foreach ($name in 'SteamPath', 'InstallPath') {
            if ($item -and $item.$name) { $roots.Add(($item.$name -replace '/', '\')) }
        }
    }
    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($root in ($roots | Select-Object -Unique)) {
        $libraries.Add($root)
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries.Add(($m.Groups[1].Value -replace '\\\\', '\'))
            }
        }
    }
    return $libraries | Select-Object -Unique
}

function Find-Game {
    foreach ($library in Get-SteamLibraries) {
        $dir = Join-Path $library 'steamapps\common\ULTRAKILL'
        if (Test-Path (Join-Path $dir 'ULTRAKILL.exe')) { return (Resolve-Path $dir).Path }
    }
    return $null
}

function Ask-GameDir {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'ULTRAKILL was not found in your Steam libraries. Pick the folder that has ULTRAKILL.exe (in Steam: right-click ULTRAKILL > Manage > Browse local files).'
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { return $dialog.SelectedPath }
    return $null
}

function Finish([int]$Code) {
    if (-not $NoPause) { Say ''; Read-Host 'Press Enter to close' | Out-Null }
    exit $Code
}

try {
    Say 'VRTRAKILL installer' 'Cyan'
    if (-not $GameDir) {
        $GameDir = Find-Game
        if ($GameDir) { Say "Found ULTRAKILL in $GameDir" }
        elseif (-not $FindOnly) { $GameDir = Ask-GameDir }
    }
    if ($FindOnly) {
        if ($GameDir) { Say "Game folder: $GameDir" 'Green'; Finish 0 }
        Say 'ULTRAKILL was not found in the Steam libraries.' 'Yellow'; Finish 1
    }
    if (-not $GameDir) { Say 'No folder picked; nothing was installed.' 'Yellow'; Finish 1 }
    if (-not (Test-Path (Join-Path $GameDir 'ULTRAKILL.exe'))) {
        Say "There is no ULTRAKILL.exe in $GameDir; nothing was installed." 'Red'; Finish 1
    }
    $GameDir = (Resolve-Path $GameDir).Path
    if ($GameDir.TrimEnd('\') -eq $Source.TrimEnd('\')) {
        Say 'The zip was already extracted into the game folder, so VRTRAKILL is installed. Start SteamVR, then ULTRAKILL.' 'Green'; Finish 0
    }

    while (Get-Process ULTRAKILL -ErrorAction SilentlyContinue) {
        Say 'ULTRAKILL is running. Close it, then press Enter.' 'Yellow'
        Read-Host | Out-Null
    }

    $copied = 0; $kept = 0
    foreach ($file in Get-ChildItem $Source -Recurse -File -Force) {
        $relative = $file.FullName.Substring($Source.TrimEnd('\').Length + 1)
        if ($Installer -contains $relative) { continue }
        $target = Join-Path $GameDir $relative
        if (($KeepIfPresent -contains $relative) -and (Test-Path $target)) { $kept++; continue }
        $folder = Split-Path $target -Parent
        if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Force $folder | Out-Null }
        Copy-Item $file.FullName $target -Force
        $copied++
    }
    Say "Installed: $copied files copied into $GameDir$(if ($kept) { ", your VRTRAKILL settings kept" })." 'Green'
    if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) {
        Say 'BepInEx 5 is not in the game folder, and the mod needs it: this is the mod-only zip. Use VRTRAKILL-<version>-full.zip instead.' 'Red'
        Finish 1
    }
    Say ''
    Say 'To play:' 'Cyan'
    Say '  - in VR: start SteamVR first, then ULTRAKILL from Steam (or from your library inside VR);'
    Say '  - flat: just start ULTRAKILL. Without SteamVR running the game starts on the monitor as usual.'
    Say 'The first start takes a little longer: the mod sets the game up for VR.'
    Finish 0
}
catch {
    Say ('Install failed: ' + $_.Exception.Message) 'Red'
    Say 'You can also install by hand: extract the zip into the ULTRAKILL folder (see README.md).'
    Finish 1
}
