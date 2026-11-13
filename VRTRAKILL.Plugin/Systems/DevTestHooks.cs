using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Plugin.Systems
{
    // Developer test hook. When BepInEx/plugins/VRTRAKILL/VRTRAKILL_TEST_SCENE.txt exists (or the VRTRAKILL_TEST_SCENE
    // environment variable is set), the mod loads the scene named in it, for example "Level 0-1", once, shortly after
    // VR has started in the main menu. It lets a level be checked in VR without controllers: SteamVR's null headset
    // driver has none, so the menus cannot be clicked. Without the file and the variable it does nothing.
    internal static class DevTestHooks
    {
        private static bool Done;

        public static void OnVRStarted()
        {
            if (Done) return;
            string Scene = Environment.GetEnvironmentVariable("VRTRAKILL_TEST_SCENE");
            try
            {
                string File_ = Path.Combine(PluginInfo.PluginPath, "VRTRAKILL_TEST_SCENE.txt");
                if (string.IsNullOrEmpty(Scene) && File.Exists(File_)) Scene = File.ReadAllText(File_).Trim();
            }
            catch (Exception e) { Vars.Log.LogWarning("Test scene file unreadable: " + e.Message); }
            if (string.IsNullOrEmpty(Scene)) return;
            Done = true;
            Vars.Log.LogWarning($"Developer test hook: loading scene \"{Scene}\" once the main menu is up (VRTRAKILL_TEST_SCENE).");
            ModHost.Instance.StartCoroutine(LoadWhenMenuReady(Scene));
        }

        // Waits for the main menu (up to a minute), then a few more seconds, then loads the scene. Counts frames and
        // uses a Stopwatch, not WaitForSecondsRealtime, which is unreliable when started at chainloader time.
        private static IEnumerator LoadWhenMenuReady(string Scene)
        {
            System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
            while (!Vars.IsMainMenu && Clock.Elapsed.TotalSeconds < 60) yield return null;
            double Ready = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < Ready + 4) yield return null;
            Vars.Log.LogWarning($"Developer test hook: SceneHelper.LoadScene(\"{Scene}\").");
            SceneHelper.LoadScene(Scene);
        }
    }
}
