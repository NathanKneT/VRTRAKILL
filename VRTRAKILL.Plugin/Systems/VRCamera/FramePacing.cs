using UnityEngine;

namespace Plugin.Systems.VRCamera
{
    // In VR the headset sets the pace: SteamVR's compositor waits for the headset every frame. The game caps its frame
    // rate from the desktop: at start-up it sets the cap to the monitor's refresh rate (IntroViolenceScreen), and its
    // graphics settings add a frame limit and vsync on the monitor. On a 60 Hz monitor the game then ran at 60 fps
    // (16.7 ms frames, measured with SteamVR's null headset) under a 90 or 120 Hz headset, which has to reproject every
    // other frame. While VR runs, the cap and vsync stay off. Without VR nothing is changed.
    internal static class FramePacing
    {
        private static bool Logged;

        // Called every frame (ModHost); two reads when nothing changed.
        public static void Apply()
        {
            if (!UnityEngine.XR.XRSettings.isDeviceActive) return;
            if (Application.targetFrameRate == -1 && QualitySettings.vSyncCount == 0) return;
            int Cap = Application.targetFrameRate, Sync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            if (!Logged)
            {
                Logged = true;
                Vars.Log.LogInfo($"VR frame pacing: the game's desktop frame cap ({Cap}) and vsync ({Sync}) are off while VR runs; the headset sets the pace.");
            }
        }
    }
}
