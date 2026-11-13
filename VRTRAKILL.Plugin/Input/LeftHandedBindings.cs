using System;
using System.IO;
using UnityEngine;

namespace Plugin.Systems.Input
{
    // "Left handed" puts the guns in the left hand, so the buttons must swap hands too: shoot on the left trigger, punch
    // on the right one, and so on. SteamVR binds actions to physical buttons, so the mod ships a second action manifest,
    // actions_lefthanded.json, whose default bindings are the right-handed ones mirrored (tools/make-lefthanded-bindings.py).
    // The OpenVR XR plugin reads the manifest path from StreamingAssets/SteamVR/OpenVRSettings.asset when VR starts, so
    // this points that line at the manifest for the current setting, before every VR start.
    // A layout the player made in SteamVR's binding screen still wins over these defaults.
    internal static class LeftHandedBindings
    {
        private const string Key = "ActionManifestFileRelativeFilePath:";
        private const string RightHanded = @"StreamingAssets\SteamVR\actions.json";
        private const string LeftHanded = @"StreamingAssets\SteamVR\actions_lefthanded.json";

        public static void SelectManifest()
        {
            try
            {
                string Folder = Path.Combine(Application.streamingAssetsPath, "SteamVR");
                string Asset = Path.Combine(Folder, "OpenVRSettings.asset");
                if (!File.Exists(Asset)) { Vars.Log.LogWarning("OpenVRSettings.asset is missing; the button layout cannot follow \"Left handed\"."); return; }

                string Wanted = RightHanded;
                if (Vars.Config.Controllers.LeftHanded)
                {
                    if (File.Exists(Path.Combine(Folder, "actions_lefthanded.json"))) Wanted = LeftHanded;
                    else Vars.Log.LogWarning("actions_lefthanded.json is missing (reinstall the mod); the buttons stay right handed.");
                }

                string[] Lines = File.ReadAllLines(Asset);
                int Index = Array.FindIndex(Lines, L => L.TrimStart().StartsWith(Key, StringComparison.Ordinal));
                if (Index < 0) { Vars.Log.LogWarning("OpenVRSettings.asset has no action manifest line; the button layout cannot follow \"Left handed\"."); return; }
                string Indent = Lines[Index].Substring(0, Lines[Index].IndexOf(Key, StringComparison.Ordinal));
                string Line = $"{Indent}{Key} {Wanted}";
                if (Lines[Index] != Line)
                {
                    Lines[Index] = Line;
                    File.WriteAllLines(Asset, Lines);
                }
                Vars.Log.LogInfo($"Button layout: {(Wanted == LeftHanded ? "left handed (mirrored)" : "right handed")}.");
            }
            // a read-only game folder must not stop VR from starting; the buttons then keep the last layout
            catch (Exception E) { Vars.Log.LogWarning("The button layout cannot follow \"Left handed\": " + E.Message); }
        }
    }
}
