using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Plugin.Systems
{
    // Says on the desktop window why VR is not running (SteamVR closed, headset off, game updated, patches missing),
    // so a failed start is not just "the game opens flat" with the reason buried in BepInEx/LogOutput.log.
    // The most reported problem with the mod (about 17 of 171 GitHub issues) is "the game does not start in VR".
    internal sealed class StartupStatus : MonoBehaviour
    {
        // the Steam build this release was tested on; README and CHANGELOG name the same build
        public const string TestedGameBuild = "22957324";

        public enum VRState { Starting, Waiting, Running }

        public static VRState State { get; private set; } = VRState.Starting;
        public static int Attempt { get; private set; }
        public static string LastError { get; private set; } = "";
        // Harmony patch types that threw, plus transpiler patterns that matched nothing (both usually mean the game changed)
        public static int PatchProblems;
        // Steam build id read from the app manifest; null for a non-Steam copy or an unreadable manifest
        public static string GameBuild { get; private set; }

        public static bool BuildMismatch => GameBuild != null && GameBuild != TestedGameBuild;

        private GUIStyle style;

        // Reads the game build and adds the overlay to the plugin's object (BepInEx keeps it across scenes).
        public static void Install(GameObject host)
        {
            GameBuild = ReadSteamBuild();
            if (GameBuild == null) Vars.Log.LogInfo("Steam build of the game: unknown (app manifest not found).");
            else if (BuildMismatch) Vars.Log.LogWarning($"ULTRAKILL build {GameBuild} is not the tested build {TestedGameBuild}. Some VR features may be broken.");
            else Vars.Log.LogInfo($"ULTRAKILL build {GameBuild} (tested build).");
            host.AddComponent<StartupStatus>();
        }

        public static void ReportAttempt(int attempt, string error)
        {
            Attempt = attempt;
            State = VRState.Waiting;
            if (!string.IsNullOrEmpty(error)) LastError = error;
        }
        public static void ReportRunning() { State = VRState.Running; LastError = ""; }

        private bool loggedDraw;

        // Text for the notice in the headset's main menu, or null when there is nothing to say.
        public static string HeadsetWarning()
        {
            if (BuildMismatch)
                return $"ULTRAKILL was updated (build {GameBuild}). This VRTRAKILL was tested on build {TestedGameBuild}: some VR features may not work until the mod is updated.";
            if (PatchProblems > 0)
                return $"{PatchProblems} VR change(s) could not be applied to this version of ULTRAKILL. See BepInEx/LogOutput.log.";
            return null;
        }

        private void OnGUI()
        {
            string text = DesktopMessage();
            if (text == null) return;
            if (!loggedDraw) { loggedDraw = true; Vars.Log.LogInfo("Startup status shown on the desktop window."); }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    richText = false,
                };
                style.normal.textColor = Color.white;
            }
            style.fontSize = Mathf.Clamp(Screen.height / 40, 14, 40);

            float width = Mathf.Min(Screen.width - 40, style.fontSize * 48);
            float height = style.CalcHeight(new GUIContent(text), width) + style.fontSize;
            Rect rect = new Rect((Screen.width - width) / 2, 20, width, height);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0, 0, 0, .9f);
            GUI.Box(rect, text, style);
            GUI.backgroundColor = previous;
        }

        // The desktop message for the current state, or null when VR runs and nothing is wrong.
        private static string DesktopMessage()
        {
            string text = null;
            switch (State)
            {
                case VRState.Waiting when Attempt >= 2: // the first try often fails while SteamVR starts; do not flash a warning
                    text = "VRTRAKILL is waiting for SteamVR.\n"
                         + (LastError.Length > 0 ? LastError + "\n" : "")
                         + "VR starts by itself as soon as SteamVR sees the headset; you do not need to restart the game.";
                    break;
            }
            if (State == VRState.Running) return null; // the player is in the headset; the main menu notice covers the rest

            if (BuildMismatch)
                text = (text == null ? "" : text + "\n\n")
                     + $"ULTRAKILL was updated (build {GameBuild}); this VRTRAKILL was tested on build {TestedGameBuild}. Some VR features may not work.";
            if (PatchProblems > 0 && text != null)
                text += $"\n{PatchProblems} VR change(s) could not be applied (see BepInEx/LogOutput.log).";
            return text;
        }

        // Finds steamapps/appmanifest_1229490.acf next to the game's library folder and returns its buildid.
        private static string ReadSteamBuild()
        {
            try
            {
                DirectoryInfo game = Directory.GetParent(Application.dataPath);               // ...\steamapps\common\ULTRAKILL
                DirectoryInfo steamapps = game?.Parent?.Parent;                                // ...\steamapps
                if (steamapps == null) return null;
                string manifest = Path.Combine(steamapps.FullName, "appmanifest_1229490.acf");
                if (!File.Exists(manifest)) return null;
                Match m = Regex.Match(File.ReadAllText(manifest), "\"buildid\"\\s+\"(\\d+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch (Exception e)
            {
                Vars.Log.LogDebug("Could not read the Steam app manifest: " + e.Message);
                return null;
            }
        }
    }
}
