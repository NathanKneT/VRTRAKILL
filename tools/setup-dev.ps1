# One-time dev setup on a machine without Visual Studio or the .NET Framework 4.7.2 Developer Pack.
#   - .NET SDK 8 per user (no admin), in %LOCALAPPDATA%\Microsoft\dotnet, if no SDK is found
#   - .NET Framework 4.7.2 reference assemblies from NuGet, in .tools/netfx-ref (Directory.Build.props picks them up)
#   - BepInEx 5.4.23.5 x64 in the game folder, if missing (official release, SHA256 checked)
# Usage: pwsh tools/setup-dev.ps1 [-ULTRAKILLDir <game folder>]
param(
    [string]$ULTRAKILLDir = $(if ($env:ULTRAKILLDir) { $env:ULTRAKILLDir } else { 'C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL' })
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Tools = Join-Path $Repo '.tools'
New-Item -ItemType Directory -Force $Tools | Out-Null

# .NET SDK
$LocalDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
$haveSdk = $false
foreach ($d in @((Get-Command dotnet -ErrorAction SilentlyContinue).Source, $LocalDotnet)) {
    if ($d -and (Test-Path $d) -and (& $d --list-sdks 2>$null)) { $haveSdk = $true; Write-Host "SDK found: $d" }
}
if (-not $haveSdk) {
    $installer = Join-Path $Tools 'dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    if ((Get-AuthenticodeSignature $installer).Status -ne 'Valid') { throw 'dotnet-install.ps1 signature is not valid' }
    & $installer -Channel 8.0 -InstallDir (Split-Path $LocalDotnet) -NoPath
}

# .NET Framework 4.7.2 reference assemblies
$RefDir = Join-Path $Tools 'netfx-ref'
if (-not (Test-Path (Join-Path $RefDir 'build\.NETFramework\v4.7.2'))) {
    $pkg = Join-Path $Tools 'netfx472ref.zip'
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net472/1.0.3' -OutFile $pkg -UseBasicParsing
    Expand-Archive $pkg -DestinationPath $RefDir -Force
    Remove-Item $pkg
}
Write-Host "Reference assemblies: $RefDir"

# BepInEx in the game folder
if (-not (Test-Path (Join-Path $ULTRAKILLDir 'ULTRAKILL.exe'))) { throw "ULTRAKILL not found at $ULTRAKILLDir" }
if (-not (Test-Path (Join-Path $ULTRAKILLDir 'BepInEx\core\BepInEx.dll'))) {
    $zip = Join-Path $Tools 'BepInEx_win_x64_5.4.23.5.zip'
    Invoke-WebRequest 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip' -OutFile $zip -UseBasicParsing
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4') { throw "BepInEx zip SHA256 is $hash" }
    Expand-Archive $zip -DestinationPath $ULTRAKILLDir -Force
}
Write-Host "BepInEx: $(Join-Path $ULTRAKILLDir 'BepInEx')"
Write-Host 'Done. Build with: dotnet build VRTRAKILL.sln -c Release -p:ULTRAKILLDir="<game folder>"  then  pwsh tools/deploy-local.ps1'
