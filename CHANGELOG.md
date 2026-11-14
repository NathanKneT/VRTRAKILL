# Changelog

## 2.0.0

A new version of motaweh's 1.0.2: the problems players reported most are fixed (from the 171 issues of the original mod), and the VR feel goes further (buttons inside the game, prompts that name your controller's buttons, layouts for more headsets, left-handed play, controller feedback, HUD on the wrist, auto height, settings in the headset):

- VR start (the most reported problem, "the game does not start in VR"):
  - The game no longer freezes on the boot screen when SteamVR or the headset is not ready. A background check asks SteamVR whether it runs and sees a headset, and VR starts only then.
  - VR starts by itself as soon as the headset is ready, at any time. You no longer need to restart the game, and there is no 3-minute limit.
  - If SteamVR is not running when the game starts, the game starts it the way a VR game does, with the game as SteamVR's startup app (as before).
  - When SteamVR has only just started, VR waits a few seconds before starting, so SteamVR Home or Room Setup, which SteamVR may still be launching, do not take VR away from the game.
  - If SteamVR stops VR for the game later (another VR app started, SteamVR closed), the desktop window says so instead of leaving a frozen headset image.
  - The menu laser no longer throws an error every frame while VR is not running.
  - When VR starts after the game (headset turned on later), the hand trackers and the SteamVR plugin are set up then too; before, a failed first try left them broken for the session.
  - If SteamVR stops answering (for example its "Unexpected SteamVR Error" with the headset unplugged), the desktop window says so; the game keeps running.
  - The retries really run now. Before, the game destroyed the object that ran them right after startup, so only the first try happened.
  - The desktop window says why VR is not running: SteamVR closed, no headset, or a game build other than 22957324.
  - The game keeps running when its window loses focus, so the headset image no longer freezes when a SteamVR window or another program takes focus.
- Movement: "Movement multiplier" now defaults to 1 (the game's speed). The old default 0.575 also lowered jumps to about a third of their height and made some tutorial jumps impossible. Jump power has its own "Jump multiplier" (default 1).
- Punch: a 2 m/s swing is enough (was 3.75 m/s), and a swing a few frames before or after the press still counts. "Enable movement-based punching": false now gives a button-only punch.
- Your config file is updated once: the old defaults (movement multiplier 0.575, punch speed 7.5) move to the new ones and the new settings are added to the file. Values you changed are kept.
- "Auto height" (on by default): at the start of each level your head is put at the game's own eye height, whatever your real height, seated or standing; crouching and leaning stay real. Press H on the keyboard ("Recalibrate height") to measure again. Before, the view sat at your real height above the player's body, which gave "constant crouch", "too tall" and seated-play problems.
- New "Height offset (meters)" moves the view and the hands up or down on top of that. The log prints your eye height at the first level.
- The view stays on your character. At the start of each level, and with "Recenter view" (or H), the spot where you stand in your room becomes the character's position. Before, the view sat wherever your head was relative to the centre of the SteamVR play area, often far from the character (2.4 m in the PS VR2 test), so walking up to a wall put the view inside it and the headset showed black. When you lean or step toward a wall, the view now stops a few centimetres before it.
- Full frame rate in the headset. The game caps its frame rate from the desktop: the monitor's refresh rate at start-up, plus its own frame limit and vsync. On a 60 Hz monitor it ran at 60 fps under a 90 or 120 Hz headset, which then has to reproject. While VR runs the cap and vsync are now off and SteamVR paces the frames (measured with the null headset: 16.7 ms frames before, 7.2 ms after). Your desktop settings are not changed.
- VR settings in the headset. A "VR" column next to the pause menu, made from the game's own buttons, changes the main settings without leaving the game: turn mode (smooth, snap 30/45/90), turn speed, UI size, comfort vignette, HUD on the wrist or floating, rumble strength (with a preview pulse), height offset, recenter view, punch by swing or by button, and left handed. Changes apply at once and are saved to the config file. The HUD can now switch between the wrist and floating while playing.
- Optional comfort vignette ("Comfort vignette", off by default): the edge of the view darkens while you move fast or turn with the stick. Drawn on top of each eye, so the game's own image is untouched.
- Controller feedback. Each hand vibrates on its own: before, only the strongest rumble played, on one controller, so firing while sliding lost the slide. New: a tick in the hand whose shot or punch lands (heavy hits tick harder), a knock on a kill, a thud in both hands when hurt, and ticks when the menu laser reaches or presses a button. New settings: "Rumble strength", "Hit and kill rumble", "Damage rumble", "Menu rumble". Vibration is now sent with a real frequency (it was sent the strength as frequency).
- VR buttons no longer need the ULTRAKILL window in front (#117 and the old wiki's "keep the ULTRAKILL window active"). They press a keyboard and a mouse that exist only inside the game's Input System, so nothing reaches Windows or other programs, and no Windows shortcut can fire. Your game key bindings still apply. Checked in Level 1-1 with another window in front: weapon slot, weapon wheel scroll, shoot, jump, pause, unpause, whiplash and punch all work; with the old Windows key presses none did. "Windows key presses" in the controller settings brings back the old way if a button misbehaves.
- Task Manager no longer opens mid-fight. Slide + dash + pause sent Ctrl+Shift+Esc to Windows.
- VR buttons are not sent to other programs when the game window does not have focus, and held buttons are released when the game loses focus or quits.
- Left handed works (#62 and 3 more reports). The buttons swap hands too: shoot on the left trigger, punch on the right, move with the right stick, Cross/A become Square/X and so on, through mirrored default layouts for PS VR2, Quest, Index, Vive and WMR that the mod picks when VR starts. The arm and the guns are no longer swapped twice, and the revolver is no longer turned around in the left hand. The setting is now called "Left handed"; the old "Left handed (BROKEN)" value is kept. A layout you made yourself in SteamVR's binding screen still wins.
- The game's prompts name your controller's buttons. The prompts, the tutorial and the hints named the keys of the keyboard the mod presses (SPACE, LEFT SHIFT). They now name the button on the controller you hold, the way that controller labels it: Cross, R2 or L3 on PS VR2, A or Right trigger on Quest and Pico, D-pad down on Steam Frame, Left trackpad up on a Vive wand. With "Left handed" on they follow the mirrored layout. Flat play and the weapon slot keys keep the game's own text.
- Steam Frame and Pico layouts. The Steam Frame and Pico controllers get their own default layouts, checked against the input profiles of SteamVR's Steam Frame and Steam Link drivers, each with a left-handed version. Through Virtual Desktop a Pico uses the Quest layout.
- The pause menu shows in levels. It was drawn but never seen: menus and the floating HUD sit 72 m away, and the UI camera used the level's occlusion culling, so a wall between you and that point hid them. That is also why the floating HUD "sometimes disappears" (noted by the FRAUD fork). The UI camera no longer uses occlusion culling. Checked in Level 1-1: the menu shows, and stays in place when you turn.
- Health and stamina are on your arm-hand wrist, like a watch, instead of floating low in your view ("HUD on wrist" in the UI interaction settings; false brings back the floating panel). UI held close to the eyes is no longer clipped.
- The wrist panel stays on the wrist while you move and turn. It is part of the main HUD canvas, which moves with the view every frame and could carry the panel after it was placed, so it trailed behind the hand (PS VR2 test). It is now placed again just before each frame is drawn.
- A smaller interface. Menus and the floating HUD spanned about 80 degrees, which put the style meter and other edge panels where headset lenses blur ("the UI is too close, I can't read it", PS VR2 test). They now span about 56 degrees, and "UI size" in the VR column (75% to 156%) changes it at once. Config files with the old default move to the new one.
- The shop and other in-world screens follow the laser. They picked the button at the centre of your view, so the highlight and the click went where you looked while the laser pointed elsewhere (PS VR2 test, shop).
- The far scenery of the Limbo levels (and the space sky) is the same in both eyes. The game renders it as a picture just before a camera draws; only the left eye rendered it, so the right eye showed the left eye's picture, with other trees and clouds in each eye (PS VR2 test, 1-1).
- The New Blood logo at start-up fits in the view. It is a video the game draws over its camera's whole view, so it filled each eye edge to edge and "NEW BLOOD INTERACTIVE" ran past both sides (PS VR2 test). It now plays on a floating panel at the size of the menus.
- The crosshair sits on the real shot line. Every gun fires from the gun hand, but the crosshair was placed from the gun rig, or from the model for the rocket launcher and jackhammer: measured in game it was 3 degrees off for every gun and 5.6 for the rocket launcher ("rocket launcher off-centred", #165, #195). Now 0.
- The crosshair sits at the depth of what you aim at. It floated 8 m ahead of the gun, so in front of a nearer wall it showed behind the wall, and in front of a shop screen the eyes saw it over another button than the one the hand pointed at (PS VR2 test). It now sits on the wall or enemy the shot line hits, or on the laser's point in menus and shops, keeps the same size to the eye, and glides between near and far.
- The rocket launcher model sits in the gun hand like the other guns. It was drawn about 30 cm toward the middle of the view and up from the hand (#195).
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
