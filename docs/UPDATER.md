# Update check and auto update

Once per game start, a few seconds after launch, the mod asks GitHub for the latest VRTRAKILL release. It sends one plain GET request to `https://api.github.com/repos/motaweh/VRTRAKILL/releases/latest` with the header `User-Agent: VRTRAKILL/<your version>`. It sends no personal data, no game data and no ID. If you are offline or GitHub does not answer within 10 seconds, the check is skipped until the next start. Prereleases are ignored.

The `"Auto update"` setting in `VRTRAKILL_Config.json` controls what happens:

- `"Notify"` (default): if a newer release exists, the log says so and a short notice shows at the bottom of the main menu for 15 seconds. Nothing is downloaded.
- `"Download"`: the mod also downloads the update and installs it at the next start (below).
- `"Off"`: no request is sent.

In a mod manager install (r2modman, Gale), `"Download"` works like `"Notify"`. Update the mod in the mod manager.

## How a download is checked and installed

1. The mod downloads `VRTRAKILL-<version>-modonly.zip`, `SHA256SUMS.txt` and `SHA256SUMS.txt.sig` into `BepInEx/VRTRAKILL_update/staging/`.
2. It checks that `SHA256SUMS.txt.sig` is a valid signature of `SHA256SUMS.txt` made with the VRTRAKILL release key, and that the zip matches its SHA-256 line. If a check fails, the download is deleted.
3. Nothing is installed while the game runs. At the next start, before any plugin loads, VRPatcher checks the files again and installs them.
4. It moves the files it replaces to `BepInEx/VRTRAKILL_update/backup/<old version>/`. If a file cannot be written, it puts the old files back. Your `VRTRAKILL_Config.json` is kept.
5. The result is in `BepInEx/VRTRAKILL_update/last_update.txt` and in `BepInEx/LogOutput.log`. A new `VRPatcher.dll` takes effect from the start after the install.

An install only writes to `BepInEx/plugins/VRTRAKILL/`, `BepInEx/patchers/` and the mod's folders under `ULTRAKILL_Data`. A zip with any other path is rejected. Files that are not in the new zip are left in place.

If an install fails, that version is not downloaded again until you delete `BepInEx/VRTRAKILL_update/failed_<version>.txt`. To remove the updater's files, delete `BepInEx/VRTRAKILL_update/`.
