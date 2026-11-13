# Copies the last Release build and the payload into the game folder, for testing.
# Keeps an existing BepInEx/plugins/VRTRAKILL/VRTRAKILL_Config.json (your settings).
# Usage: pwsh tools/deploy-local.ps1 [-ULTRAKILLDir <game folder>] [-Build]
param(
    [string]$ULTRAKILLDir = $(if ($env:ULTRAKILLDir) { $env:ULTRAKILLDir } else { 'C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL' }),
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (Get-Process ULTRAKILL -ErrorAction SilentlyContinue) { throw 'Close ULTRAKILL first.' }
if (-not (Test-Path (Join-Path $ULTRAKILLDir 'BepInEx\core\BepInEx.dll'))) { throw "BepInEx is not installed in $ULTRAKILLDir (run tools/setup-dev.ps1)" }

if ($Build) {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path $local) { $dotnet = $local }
    & $dotnet build (Join-Path $Repo 'VRTRAKILL.sln') -c Release -nologo -v m "-p:ULTRAKILLDir=$ULTRAKILLDir"
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
}

$PluginDir = Join-Path $ULTRAKILLDir 'BepInEx\plugins\VRTRAKILL'
$Config = Join-Path $PluginDir 'VRTRAKILL_Config.json'
$saved = if (Test-Path $Config) { Get-Content $Config -Raw } else { $null }

Copy-Item (Join-Path $Repo 'ULTRAKILL_COPYTOROOT\*') $ULTRAKILLDir -Recurse -Force
Copy-Item (Join-Path $Repo 'VRTRAKILL.Plugin\bin\Release\VRTRAKILL.dll') $PluginDir -Force
Copy-Item (Join-Path $Repo 'bin\VRTRAKILL.Utilities.dll') $PluginDir -Force
Copy-Item (Join-Path $Repo 'bin\Release\CopyTo\ULTRAKILL\BepInEx\patchers\VRPatcher.dll') (Join-Path $ULTRAKILLDir 'BepInEx\patchers') -Force

if ($saved) { [System.IO.File]::WriteAllText($Config, $saved) }
Write-Host "Deployed to $ULTRAKILLDir"
