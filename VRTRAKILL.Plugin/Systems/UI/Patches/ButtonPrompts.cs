using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Valve.VR;

namespace Plugin.Systems.UI.Patches
{
    // The game's prompts ("press ... to jump", the tutorial, hints, the controls lists) name the binding of an action
    // through InputManager.GetBindingString. In VR that named the keys of the virtual keyboard the mod presses (SPACE,
    // LEFT SHIFT). It now names the button on the controller in the player's hands, the way that controller labels it:
    // Cross and R2 on PS VR2 Sense, A and the right trigger on Quest Touch, the d-pad on Steam Frame, and so on. The
    // button comes from the binding file the mod ships for that controller type (the left-handed one when "Left
    // handed" is on), so the prompt always matches the default layout. A layout the player made in SteamVR's binding
    // screen is not read. Unknown controllers, actions without a VR binding (weapon slots) and flat play keep the
    // game's own text.
    [HarmonyPatch]
    internal static class ButtonPrompts
    {
        // the game's action names (InputActions: "Primary Fire", "Change Fist"...; spaces are ignored) and the mod's
        // SteamVR actions that press their keys
        private static readonly Dictionary<string, string> GameToVR = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Move", "movement" }, { "Look", "turn" }, { "WheelLook", "weaponwheelscroll" },
            { "Jump", "jump" }, { "Slide", "slide" }, { "Dodge", "dash" },
            { "Punch", "punch" }, { "Punch(Feedbacker)", "punch" }, { "Punch(Knuckleblaster)", "punch" },
            { "Hook", "whiplash" }, { "ChangeFist", "swaphand" },
            { "PrimaryFire", "shoot" }, { "SecondaryFire", "altshoot" },
            { "NextVariation", "changeweaponvariation" }, { "PreviousVariation", "openweaponwheel" }, { "LastUsedWeapon", "openweaponwheel" },
            { "Pause", "escape" },
        };

        // developer test hook only: names prompts for this controller type without a controller connected
        internal static string TestControllerType;

        private static readonly Dictionary<string, Dictionary<string, string>> Labels = new Dictionary<string, Dictionary<string, string>>();
        private static string CachedType;
        private static float CachedAt = -10f;
        private static bool LoggedError;

        // Runs before the game's method: returns the VR button when there is one. The game's method throws when no input
        // device was used yet (it asks its control schemes about LastButtonDevice, null): in VR the menus are used with
        // the laser, so the first prompt of a level could come before any button press. The device is then set to the
        // virtual keyboard the VR buttons press.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetBindingString), typeof(string))]
        static bool NameTheVRButton(InputManager __instance, string nameOrId, ref string __result)
        {
            if (TestControllerType == null && !UnityEngine.XR.XRSettings.isDeviceActive) return true;
            try
            {
                if (__instance.LastButtonDevice == null)
                    __instance.LastButtonDevice = (InputDevice)Input.VirtualInput.KeyboardDevice ?? Keyboard.current;
                InputAction Action = __instance.InputSource?.Actions?.FindAction(nameOrId);
                if (Action == null || !GameToVR.TryGetValue(Action.name.Replace(" ", ""), out string VRAction)) return true;
                string Type = ControllerType();
                if (Type == null) return true;
                if (!LabelsFor(Type).TryGetValue(VRAction, out string Label)) return true;
                __result = Label;
                return false;
            }
            catch (Exception E)
            {
                if (!LoggedError) { LoggedError = true; Vars.Log.LogWarning("Button prompts keep the game's text: " + E.Message); }
                return true;
            }
        }

        // The SteamVR controller type of the hand controllers ("playstation_vr2_sense", "oculus_touch"...), or null.
        private static string ControllerType()
        {
            if (TestControllerType != null) return TestControllerType;
            if (Time.unscaledTime - CachedAt < 2f) return CachedType;
            CachedAt = Time.unscaledTime;
            CachedType = null;
            CVRSystem System = OpenVR.System;
            if (System == null) return null;
            var SB = new StringBuilder(64);
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (System.GetTrackedDeviceClass(i) != ETrackedDeviceClass.Controller) continue;
                ETrackedPropertyError Error = ETrackedPropertyError.TrackedProp_Success;
                SB.Length = 0;
                System.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_ControllerType_String, SB, (uint)SB.Capacity, ref Error);
                if (Error == ETrackedPropertyError.TrackedProp_Success && SB.Length > 0) { CachedType = SB.ToString(); break; }
            }
            return CachedType;
        }

        // Action -> button label for a controller type, read once from the manifest in use and its binding file.
        private static Dictionary<string, string> LabelsFor(string Type)
        {
            bool Left = Vars.Config.Controllers.LeftHanded;
            string Key = Type + (Left ? "|left" : "|right");
            if (Labels.TryGetValue(Key, out var Cached)) return Cached;
            var Result = new Dictionary<string, string>();
            Labels[Key] = Result;
            string Folder = Path.Combine(Application.streamingAssetsPath, "SteamVR");
            JObject Manifest = JObject.Parse(File.ReadAllText(Path.Combine(Folder, Left ? "actions_lefthanded.json" : "actions.json")));
            string File_ = null;
            foreach (JToken D in Manifest["default_bindings"])
                if ((string)D["controller_type"] == Type) { File_ = (string)D["binding_url"]; break; }
            if (File_ == null) return Result; // a controller this mod has no layout for: the game's text stays
            JObject Binding = JObject.Parse(File.ReadAllText(Path.Combine(Folder, File_)));
            var Rank = new Dictionary<string, int>();
            foreach (JToken Source in Binding["bindings"]?["/actions/default"]?["sources"] ?? new JArray())
            {
                string SourcePath = (string)Source["path"], Mode = (string)Source["mode"];
                if (!(Source["inputs"] is JObject Inputs)) continue;
                foreach (var Input in Inputs)
                {
                    string Output = (string)Input.Value["output"];
                    if (Output == null) continue;
                    string VRAction = Output.Substring(Output.LastIndexOf('/') + 1).ToLowerInvariant();
                    // a plain press reads better than a hold or a double tap (Options before "double-tap Circle")
                    int R = Input.Key == "held" || Input.Key == "double" ? 1 : 0;
                    if (Rank.TryGetValue(VRAction, out int Old) && Old <= R) continue;
                    Rank[VRAction] = R;
                    Result[VRAction] = Label(Type, SourcePath, Mode, Input.Key);
                }
            }
            return Result;
        }

        // "/user/hand/right/input/cross", "button", "click" -> "Cross"; "/user/hand/left/input/joystick", "joystick",
        // "click" -> "Left stick click"; "/user/hand/right/input/b", "button", "held" -> "Hold B".
        internal static string Label(string Type, string SourcePath, string Mode, string Input)
        {
            bool LeftHand = SourcePath.Contains("/hand/left/");
            string Side = LeftHand ? "Left" : "Right";
            string Component = SourcePath.Substring(SourcePath.IndexOf("/input/", StringComparison.Ordinal) + 7);
            string Name;
            if (Type == "playstation_vr2_sense")
            {
                switch (Component)
                {
                    case "left_stick": Name = Input == "click" ? "L3" : "Left stick"; break;
                    case "right_stick": Name = Input == "click" ? "R3" : "Right stick"; break;
                    default: Name = PSNames.TryGetValue(Component, out string N) ? N : Title(Component); break;
                }
            }
            else
            {
                bool OneHanded = OneHandedButtons.TryGetValue(Type, out var Set) && Set.Contains(Component);
                string Base = OtherNames.TryGetValue(Component, out string N) ? N : Title(Component);
                // letters stay capitals: "Right A" on Index, where both hands have A and B
                Name = OneHanded ? Base : Side + " " + (Base.Length == 1 ? Base : Base.ToLowerInvariant());
                if ((Component == "joystick" || Component == "thumbstick" || Component == "trackpad") && Input == "click") Name += " click";
                // a pad or stick used as four buttons (Vive trackpad): "Left trackpad up", "Right trackpad click"
                if (Mode == "dpad" && Compass.TryGetValue(Input, out string Direction)) return Name + " " + Direction;
            }
            switch (Input)
            {
                case "held": return "Hold " + Name;
                case "double": return "Double-tap " + Name;
                case "touch": return Name + " touch";
                default: return Name;
            }
        }

        private static readonly Dictionary<string, string> Compass = new Dictionary<string, string>
        {
            { "north", "up" }, { "south", "down" }, { "east", "right" }, { "west", "left" }, { "center", "click" },
        };
        private static readonly Dictionary<string, string> PSNames = new Dictionary<string, string>
        {
            { "cross", "Cross" }, { "circle", "Circle" }, { "square", "Square" }, { "triangle", "Triangle" },
            { "l1", "L1" }, { "l2", "L2" }, { "r1", "R1" }, { "r2", "R2" }, { "options", "Options" }, { "create", "Create" },
        };
        private static readonly Dictionary<string, string> OtherNames = new Dictionary<string, string>
        {
            { "a", "A" }, { "b", "B" }, { "x", "X" }, { "y", "Y" }, { "trigger", "Trigger" }, { "grip", "Grip" },
            { "joystick", "Stick" }, { "thumbstick", "Stick" }, { "trackpad", "Trackpad" }, { "bumper", "Bumper" },
            { "application_menu", "Menu" }, { "menu", "Menu" }, { "view", "View" },
            { "dpad_up", "D-pad up" }, { "dpad_down", "D-pad down" }, { "dpad_left", "D-pad left" }, { "dpad_right", "D-pad right" },
        };
        // buttons that exist on one hand only, so the label needs no "Left"/"Right"
        private static readonly Dictionary<string, HashSet<string>> OneHandedButtons = new Dictionary<string, HashSet<string>>
        {
            { "oculus_touch", new HashSet<string> { "a", "b", "x", "y" } },
            { "pico_controller", new HashSet<string> { "a", "b", "x", "y" } },
            { "frame_controller", new HashSet<string> { "a", "b", "x", "y", "menu", "view", "dpad_up", "dpad_down", "dpad_left", "dpad_right" } },
        };

        private static string Title(string Component) => Component.Length == 0 ? Component : char.ToUpperInvariant(Component[0]) + Component.Substring(1).Replace('_', ' ');
    }
}
