# Building and testing

## Requirements

- A .NET SDK, 7.0.400 or later. `global.json` rolls forward, so .NET 8 works.
- ULTRAKILL build 22957324 with BepInEx 5.4.23.5 installed in it.
- To build on a PC without Visual Studio: the .NET Framework 4.7.2 reference assemblies. `tools/setup-dev.ps1` gets them.

The game's DLLs are not in this repository. The build reads Assembly-CSharp, the Unity DLLs and the BepInEx core from your game folder, and publicizes Assembly-CSharp at build time.

## Setup

Run once:

```
pwsh tools/setup-dev.ps1 -ULTRAKILLDir "<game folder>"
```

It installs the .NET 8 SDK for your user if no SDK is found, puts the .NET Framework 4.7.2 reference assemblies from NuGet in `.tools/netfx-ref` (the build uses them automatically), and installs BepInEx 5.4.23.5 in the game folder if it is missing (official release, SHA-256 checked).

## Build

```
dotnet build VRTRAKILL.sln -c Release -p:ULTRAKILLDir="<game folder>"
```

The game folder comes from the `ULTRAKILLDir` property. The default is `C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL`. Set it on the command line or as an environment variable if your game is somewhere else.

The version is `VRTRAKILLVersion` in `Directory.Build.props`. It sets the plugin and assembly versions and the release zip names.

## Repository layout

| Folder | What it is |
|---|---|
| `VRTRAKILL.Plugin/` | The BepInEx plugin: VR camera and hands, input, UI, weapons, settings. |
| `VRPatcher/` | The BepInEx preloader patcher: turns on VR in `globalgamemanagers`, copies the VR files in a mod manager install, installs downloaded updates. |
| `VRTRAKILL.Utilities/` | Helpers the plugin uses: Harmony patching by namespace, Unity helpers, and the bundled WindowsInput and EZSoftBone sources. |
| `Shared/` | Code built into both the plugin and the patcher (the updater). |
| `ULTRAKILL_COPYTOROOT/` | Everything the mod installs besides its DLLs, laid out like the game folder: the SteamVR plugin and Unity XR DLLs, `openvr_api.dll`, the SteamVR actions and controller layouts. |
| `thunderstore/` | The Thunderstore package's manifest, README, changelog and icon. |
| `tools/` | Setup, local deploy, packaging, signing and the left-handed layout generator. |
| `docs/` | The headsets guide, this file and the updater notes. |

The Unity project for `vrtrakillassetbundle` is not in this repository. It is `_unityProject.7z` in the `ultra_revamp` branch of the [original repo](https://github.com/whateverusername0/VRTRAKILL/tree/ultra_revamp).

## Testing in the game

```
pwsh tools/deploy-local.ps1 -ULTRAKILLDir "<game folder>" -Build
```

builds Release and copies the build and `ULTRAKILL_COPYTOROOT` into the game folder. It keeps your `VRTRAKILL_Config.json`.

The mod writes to `BepInEx/LogOutput.log`. Unity writes `%USERPROFILE%\AppData\LocalLow\Hakita\ULTRAKILL\Player.log`.

### Without a headset

SteamVR's null driver acts as a headset with no controllers, which is enough to start VR, load levels and check the headset view. Turn it on in `Steam\config\steamvr.vrsettings` (`"forcedDriver": "null"` and `"activateMultipleDrivers": true` under `steamvr`, `"enable": true` under `driver_null`), and turn it off again before you use a real headset.

### Test hooks

A file `BepInEx/plugins/VRTRAKILL/VRTRAKILL_TEST_SCENE.txt` in the game folder makes the mod load a scene and run checks, logged to `BepInEx/LogOutput.log`. The first line is a scene name such as `Level 1-1`: the mod loads it once, a few seconds after VR starts in the main menu. Each further line runs one check. Without the file nothing happens; delete it when you are done.

Weapons appear from Level 1-1 on; Level 0-1 starts empty-handed.

| Line | What it does |
|---|---|
| `inputtest` | Presses weapon slot, next weapon, shoot, jump and pause through the mod's SteamVR handlers and checks the game reacted. |
| `unfocused` | Put it first: brings the Windows taskbar to the front, so the checks after it run with the game window unfocused. |
| `jumptest` | Jumps three times through the game's own jump and logs the height, walk speed and jump power. |
| `punchtest` | Presses punch through the mod's handler with simulated hand speeds and counts the punches. |
| `shortcuttest` | Holds slide and dash, then pauses. Check that Task Manager did not open. |
| `aimtest` | For each weapon in the save, the angle between the crosshair and the shot line; fires a rocket and measures it. |
| `whiplashtest` | Checks the whiplash marker and throws the whiplash through the mod's handler. |
| `haptictest` | Records the vibration sent to each hand and checks gunfire with a slide, hits, kills, damage and the menu laser ticks. |
| `prompttest` | Logs the button names the game's prompts show for every controller type, and for PS VR2 and Steam Frame left-handed. |
| `vrsettingstest` | Opens the pause menu, points at each button of the VR column and presses it. It changes the config file: back it up first. |
| `endtest` | Finishes the level through its final pit and goes on to the next one. Marks the run "major assists used" first, so no score reaches the Steam leaderboards, and puts the `Saves` folder back while the game still runs. |
| `levelsweep` | Loads every level in turn and logs whether the player and the VR eyes are there, the exceptions and the frame time. |
| `perftest` | Times the mod's per-frame methods and records frame times and SteamVR's dropped and reprojected frames for 10 s. |
| `rangeview` | Logs the eyes' near and far planes and fog against the game camera's, and turns the view four ways for captures. |
| `pauseview` | Opens the pause menu, turns 60 degrees and logs where the menu is. |
| `pausedump` | Logs the pause menu's object tree. |
| `wristview` | Raises the arm hand like reading a watch, for a capture of the wrist HUD. |
| `weaponview` | Holds each weapon family for 4 s at a fixed hand pose, for captures. |

The view checks log a line such as `Weapon view: <name>` when the headset window is ready to capture. With `Left handed` on, the hand poses are mirrored.

## Controller layouts

The SteamVR action manifest and the default layout of each controller are in `ULTRAKILL_COPYTOROOT/ULTRAKILL_Data/StreamingAssets/SteamVR/` (`actions.json`, `bindings_<controller>.json`).

The left-handed layouts (`bindings_*_lefthanded.json` and `actions_lefthanded.json`) are generated from the right-handed ones. After changing a layout, run from the repository root:

```
python tools/make-lefthanded-bindings.py
```

It mirrors buttons and sticks with each controller driver's own mirror table, leaves hand poses, finger tracking and rumble on their physical hand, and checks every path against the SteamVR input profiles installed on the PC.

The game's button prompts read the same files (`VRTRAKILL.Plugin/Systems/UI/Patches/ButtonPrompts.cs`), so a changed layout changes the prompts too. A new controller type needs its button names there if they are not plain letters.

## Release zips

```
pwsh tools/package.ps1 -ULTRAKILLDir "<game folder>"
```

builds Release, downloads the BepInEx 5.4.23.5 x64 zip from the BepInEx GitHub release, checks its SHA-256, and writes `VRTRAKILL-<version>-full.zip`, `VRTRAKILL-<version>-modonly.zip`, `SHA256SUMS.txt` and `SHA256SUMS.txt.sig` to `artifacts/`.

`tools/sign-release.ps1` signs `SHA256SUMS.txt` with the release key, which is kept outside the repository (`%USERPROFILE%\.vrtrakill`, protected with Windows DPAPI). Use `-NoSign` for a local test build; the in-game updater does not install unsigned zips. `tools/backup-signing-key.ps1` backs the key up to 1Password and restores it on a new Windows profile (`-Restore`).

## Thunderstore package

```
pwsh tools/package-thunderstore.ps1 -ULTRAKILLDir "<game folder>"
```

builds Release and writes `VRTRAKILL-<version>.zip` to `artifacts/thunderstore/`. `version_number` in `thunderstore/manifest.json` must match `VRTRAKILLVersion`. A mod manager can only install into `BepInEx/`, so the package puts the `ULTRAKILL_Data` files in `BepInEx/patchers/<team>-VRTRAKILL/GameData/`. VRPatcher copies them into the game's `ULTRAKILL_Data` at start-up when they are missing or different. With the zip install that folder does not exist and VRPatcher skips the copy.

## Releasing from a new repository

The updater, the Thunderstore page and the signature check name the repository that publishes the releases. Before the first release from another repository:

1. Set `LatestReleaseUrl` and `ReleasesPageUrl` in `Shared/Updates/UpdateLayout.cs`.
2. Set `website_url` in `thunderstore/manifest.json`, and the GitHub links in `thunderstore/README.md`.
3. Run `pwsh tools/sign-release.ps1 -Init` once. It makes a new release key and writes its public half to `Shared/Updates/ReleaseKey.cs`. Back the key up with `tools/backup-signing-key.ps1`.

Release tags are `v<version>` (`v2.0.0`). Attach both zips, `SHA256SUMS.txt` and `SHA256SUMS.txt.sig` to the GitHub release; the updater looks for those names.
