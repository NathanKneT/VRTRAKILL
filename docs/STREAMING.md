# Recording and streaming

VRTRAKILL can show a view made for viewers on your monitor: the streamer view. This page explains how to turn it on, what it shows, and how to record it cleanly.

## Turn it on

- Press `T` with the ULTRAKILL window in front ("Toggle streamer view" in `VRTRAKILL_Config.json`), or
- pause the game in the headset and select **STREAMER VIEW** in the VR column.

The choice is saved. Off, the monitor shows the headset mirror (one eye, moving with every head movement).

## What it shows

- A camera at the middle of your eyes. Head turns are smoothed (less during fast turns, so a quick look stays readable), the horizon stays level, and the game's own motion (dashes, slides, the whiplash) is not delayed.
- A 16:9 picture with a 90 degree field of view, letterboxed to the window.
- No comfort vignette or low health glow: they are for your eyes only.
- A HUD with health, stamina and the style rank, since in VR they sit on your wrist and at the edge of your view.
- With **OVERLAY** on ALWAYS (the default):
  - a small ring where your eyes look (PS VR2 with the PSVR2 Toolkit and its eye tracking);
  - each eye-aimed parry: the ring pulses, "EYE-AIMED PARRY" shows, and a dotted line goes from your fist to where the projectile was sent;
  - "PARRY" when the parry warning ticks in your hand;
  - what the adaptive triggers feel like right now (R2 CLICK, CHARGE 2/4, READY, LOCKED...), since viewers cannot feel them.
- OVERLAY on PARRIES shows the ring and the parry path only around parries; OFF shows neither (the HUD has its own setting).

None of this is drawn in the headset.

## Settings

In the "Streamer view" section of `BepInEx/plugins/VRTRAKILL/VRTRAKILL_Config.json` (close the game first):

| Setting | Default | What it does |
|---|---|---|
| `On` | `false` | The streamer view instead of the headset mirror. |
| `Field of view (horizontal degrees)` | `90` | Wider shows more around you; 70 to 90 suits most videos. |
| `Turn smoothing` | `"Medium"` | `"Off"`, `"Low"`, `"Medium"` or `"High"`. More is calmer to watch but lags more behind your head. |
| `Level horizon (0 to 1)` | `1.0` | 1 keeps the horizon level; 0 rolls with your head. |
| `Frame rate (0 = headset rate)` | `60` | How often the view is drawn. Pick a rate that divides your headset's: 60 at 120 Hz, 45 or 90 at 90 Hz. |
| `Height in pixels` | `1080` | The picture size: 1080 is 1920x1080, 1440 is 2560x1440. Bigger costs the headset more. |
| `HUD` | `"Medium"` | `"Off"`, `"Small"`, `"Medium"` or `"Large"`. |
| `Overlay` | `"Always"` | `"Always"`, `"Parries"` or `"Off"`. |

## Recording with OBS

1. In SteamVR, set the headset to 120 Hz if your PC holds it, and keep the streamer view at 60: every second headset frame, so the video is smooth. At 90 Hz, use 45 or 90.
2. Run ULTRAKILL in a borderless window or full screen at the size you record (1920x1080 for a 1080p video). Keep the window open, not minimised: a minimised window has no picture to capture.
3. In OBS, add a **Game Capture** source on the ULTRAKILL window (not a Window Capture, which is slower and can miss frames).
4. Output: 1080p60 at about 12 Mbps, or 1440p60 at about 24 Mbps (YouTube's recommended upload rates). Live streams on Twitch usually run at about 6 Mbps.
5. If the headset drops frames while recording, lower `Height in pixels` or the frame rate, or turn off SteamVR's Motion Smoothing.

To show the raw headset view as well, the OBS "OpenVR Capture" plugin captures one eye directly; put it in a corner of your scene.

## Eye tracking on PS VR2

The gaze ring needs the [PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit) with eye tracking calibrated (for example with [PSVR2EyeTrackingCalibration](https://github.com/tobexeon/PSVR2EyeTrackingCalibration)). `BepInEx/LogOutput.log` says "Eye tracking: gaze received" once the gaze arrives. Without it, the ring does not show and parries aim with your head.

The PSVR2 Toolkit's README says its eye tracking data must not be used for commercial purposes. Read its terms before you monetise footage that shows your gaze.
