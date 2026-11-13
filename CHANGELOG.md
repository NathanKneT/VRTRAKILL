# Changelog

## 1.1.0

Fixes for the problems players reported most (from the 171 issues of the original mod):

- VR start (the most reported problem, "the game does not start in VR"):
  - The game no longer freezes on the boot screen when SteamVR or the headset is not ready. A background check asks SteamVR whether it runs and sees a headset, and VR starts only then.
  - VR starts by itself as soon as the headset is ready, at any time. You no longer need to restart the game, and there is no 3-minute limit.
  - If SteamVR is not running, the mod asks Steam to start it.
  - The retries really run now. Before, the game destroyed the object that ran them right after startup, so only the first try happened.
  - The desktop window says why VR is not running: SteamVR closed, no headset, or a game build other than 22957324.
  - The game keeps running when its window loses focus, so the headset image no longer freezes when a SteamVR window or another program takes focus.
- Movement: "Movement multiplier" now defaults to 1 (the game's speed). The old default 0.575 also lowered jumps to about a third of their height and made some tutorial jumps impossible. Jump power has its own "Jump multiplier" (default 1).
- Punch: a 2 m/s swing is enough (was 3.75 m/s), and a swing a few frames before or after the press still counts. "Enable movement-based punching": false now gives a button-only punch.
- Your config file is updated once: the old defaults (movement multiplier 0.575, punch speed 7.5) move to the new ones and the new settings are added to the file. Values you changed are kept.
- New "Height offset (meters)" moves the view and the hands up or down, for seated play or a view that is too low or too high. The log prints your eye height at the first level.
- Task Manager no longer opens mid-fight. Slide + dash + pause sent Ctrl+Shift+Esc to Windows.
- VR buttons are not sent to other programs when the game window does not have focus, and held buttons are released when the game loses focus or quits.
- Left handed: the arm and the guns are no longer swapped twice (flipped revolver). The buttons stay right handed; swap them in SteamVR's binding screen.
- A small blue marker ahead of the arm hand shows where the whiplash will go while it is ready to throw ("Whiplash aim marker" in the CBS settings).
- White and black screen flashes no longer grow without limit over a session.
- The end-of-level rank screen no longer fails when its canvas is not where the mod expected.

## 1.0.2

- The weapon wheel stays open while you hold B (Quest) or Circle (PS VR2). Before, it closed after about 1 second.

## 1.0.1

- Controls follow the common VR shooter layout (Meta's controller guidance and ULTRAKILL's own gamepad layout). The same logic applies to Index, Vive and WMR.
- Jump is on Cross on PS VR2 and A on Quest.
- Dash is on the left stick click.
- Change weapon variation is on Triangle (PS VR2) or Y (Quest).
- Whiplash is on L1 or the left grip.
- Change fist (Feedbacker / Knuckleblaster) is on Square or X.
- The right stick only turns. It no longer switches weapons. Use the weapon wheel. In menus the right stick still scrolls.
- One press of Circle (PS VR2) or B (Quest) goes back in menus. In play only a double press pauses. Options still pauses on PS VR2.
- Menus: Cross on PS VR2 and A on Quest select (the jump button on other controllers). The trigger selects what the laser points at, on either hand.
- Presses no longer leak from menus into the game. The select buttons do not jump, shoot or punch in a menu, or when the menu closes while you hold them.
- A small hand turn while you pull the trigger no longer cancels a click on a button in a scrolling list.
- Rockets, cannonballs and napalm fire from the gun hand.
- The log records what opens and closes the pause menu.
- Controllers no longer keep vibrating after a slide, whiplash or nailgun shot.
- A held punch no longer fires on its own while you walk or use the weapon wheel.
- Punches use stamina and cooldown like in the normal game.
- Held buttons are released when the SteamVR menu opens.
- Jackhammer impact, coin toss, dual wield and parry fixes.
- The SteamVR NoData errors at startup are gone.
- A broken or empty config file no longer disables the mod.
- Turning off "Draw controller models" no longer breaks the hands.
- The weapon wheel uses its own stick and you stop walking while it is open.
- Shop enemy and weapon previews render in the headset.
- 4-S and Clash mode show the platformer camera in the headset.
- HUD and menus that appear after a level loads are converted for VR, so the HUD stays visible.
- The desktop view toggle (T key) works.
- Faster start: VRPatcher no longer makes BepInEx rewrite Assembly-CSharp on every start.
- "Enable Controller Rumble" and "Enable Crosshair" in the config file now work.
- New option "Auto update". "Notify" (default) shows a notice in the main menu when a new release is out. "Download" (opt-in) downloads a signed update and installs it at the next start. "Off" turns the check off.
- New option "Game blood and oil stains (experimental)", off by default.
- Thunderstore package for r2modman and Gale. VRPatcher copies the VR game files into `ULTRAKILL_Data` at start-up, so the mod works from a mod manager profile. The zip install works as before.
- After this update, reset SteamVR to the default binding if the buttons still act like before (SteamVR Settings > Controllers > Manage Controller Bindings > ULTRAKILL > Default).

## 1.0.0

- First release for the current ULTRAKILL on Steam, build 22957324 (Unity 2022.3.29f1). No downgrade needed.
- Portal, whiplash, shotgun saw and punch patches are updated for the new game code.
- Runs through SteamVR (OpenVR) on any SteamVR headset. The mod no longer ships Oculus or OpenXR runtime files.
- OpenVR start-up retries if SteamVR is slow to start.
- Tested on PS VR2. New default bindings for PS VR2 Sense. Fixed bindings for Touch, Index, Vive wands and WMR.
- Menus work with the controller laser and the left stick.
- Rebinds from the game's controls menu reach the mod again.
- VRPatcher works with Unity 2022.3. It turns on VR in globalgamemanagers on the first start and patches it again after a game update.
- Full zip with BepInEx 5.4.23.5, and a mod-only zip.
