using Newtonsoft.Json;

#pragma warning disable IDE1006 // Naming Styles
namespace Plugin.Prefs
{
    public class NewConfig
    {
        [JsonIgnore] public UKBindings.ModifiedActions UKBinds { get; set; }
        [JsonProperty("VRTRAKILL Keybinds")] public _VRBinds VRBinds { get; set; }
        public class _VRBinds
        {
            [JsonProperty("Toggle Desktop View")] public string ToggleDV { get; set; } = "T";
            [JsonProperty("Toggle Size Adjustment")] public string ToggleAvatarSizeAdj { get; set; } = "J";
        }

        // 1 = the game's own speed. The old default 0.575 also cut jump and wall jump power, so jumps reached about a third
        // of their height (gravity is unchanged) and tutorial jumps were impossible: the most reported gameplay complaint.
        [JsonProperty("Movement multiplier")] public float MovementMultiplier { get; set; } = 1f;
        // Applies to jump and wall jump power only. Below 1 some jumps in the levels cannot be made.
        [JsonProperty("Jump multiplier")] public float JumpMultiplier { get; set; } = 1f;

        // Moves the view and the hands up (positive) or down (negative), in meters. For seated play or a view that sits
        // too low or too high; the log prints the eye height at the first level.
        [JsonProperty("Height offset (meters)")] public float HeightOffset { get; set; } = 0f;

        // Version of the defaults this file was written with; null for files from before 1.1.0. See Migrate.
        [JsonProperty("Config version")] public int? ConfigVersion { get; set; }
        // 2: new movement and punch defaults. 3: whiplash aim marker and height offset added.
        // Bump it when settings are added, so existing files are rewritten and show the new settings.
        public const int CurrentConfigVersion = 3;

        // Moves values that are still the old defaults to the new defaults, once. Returns true when the file should be
        // rewritten (which also adds settings the file does not have yet).
        public bool Migrate()
        {
            if (ConfigVersion >= CurrentConfigVersion) return false;
            if ((ConfigVersion ?? 1) < 2)
            {
                // only exact old defaults change; a value the player chose stays
                if (System.Math.Abs(MovementMultiplier - 0.575f) < 0.0001f) MovementMultiplier = 1f;
                if (System.Math.Abs(MBP.PunchingSpeed - 7.5f) < 0.0001f) MBP.PunchingSpeed = new _MBP().PunchingSpeed;
            }
            ConfigVersion = CurrentConfigVersion;
            return true;
        }

        [JsonProperty("Controller Settings")] public _Controllers Controllers { get; set; }
        public class _Controllers
        {
            [JsonProperty("Deadzone (from 0 to 1)")] public float Deadzone { get; set; } = 0.4f;
            [JsonProperty("Smooth turning speed")] public float SmoothSpeed { get; set; } = 300;
            [JsonProperty("Snap turning")] public bool SnapTurn { get; set; } = false;
            [JsonProperty("Snap turning angles")] public float SnapAngles { get; set; } = 45;

            [JsonProperty("Draw controller models")] public bool DrawControllers { get; set; } = true;
            [JsonProperty("Enable Controller Rumble")] public bool EnableHaptics { get; set; } = true;
            [JsonProperty("Left handed (BROKEN)")] public bool LeftHanded { get; set; } = false;
            [JsonProperty("Finger tracking")] public bool FingerTracking { get; set; } = false;
        }

        [JsonProperty("Enable controller-based aiming")] public bool EnableCBA { get; set; } = true;
        [JsonProperty("CBS Settings")] public _CBS CBS { get; set; }
        public class _CBS
        {
            [JsonProperty("Enable Crosshair")] public bool EnableCrosshair { get; set; } = true;
            [JsonProperty("Crosshair distance")] public float CrosshairDistance { get; set; } = 8;
            // a small marker ahead of the arm hand where the whiplash will go, shown while it is ready to throw
            [JsonProperty("Whiplash aim marker")] public bool WhiplashMarker { get; set; } = true;
        }

        // true: hold punch and swing the fist. false: the punch button alone punches, like the flat game.
        [JsonProperty("Enable movement-based punching")] public bool EnableMBP { get; set; } = true;
        [JsonProperty("MBP Settings")] public _MBP MBP { get; set; }
        public class _MBP
        {
            // twice the hand speed in m/s; 4 = a 2 m/s swing (the old 7.5 needed 3.75 m/s, and most presses did nothing)
            [JsonProperty("Required speed to punch")] public float PunchingSpeed { get; set; } = 4f;
            [JsonProperty("WHIPLASH: camera-based aiming")] public bool CameraWhiplash { get; set; } = false;
        }

        [JsonProperty("Enable VRAvatar")] public bool EnableVRBody { get; set; } = false;
        [JsonProperty("VRAvatar Settings")] public _VRBody VRBody { get; set; }
        public class _VRBody
        {
            [JsonProperty("Enable arms IK")] public bool EnableArmsIK { get; set; } = true;
            [JsonProperty("Enable legs IK")] public bool EnableLegsIK { get; set; } = true;

            // Creates the VR avatar settings with default values.
            public _VRBody()
            {

            }
        }

        [JsonProperty("UI Interaction Settings")] public _UIInteraction UIInteraction { get; set; }
        public class _UIInteraction
        {
            [JsonProperty("UI Size (from 0 to 0.1)")] public float UISize { get; set; } = 0.0625f;
            [JsonProperty("Controller-based")] public bool ControllerBased { get; set; } = true;

            [JsonProperty("Controller Lines")] public _ControllerLines ControllerLines { get; set; }
            public class _ControllerLines
            {
                [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
                [JsonProperty("Starting transparency (from 0 to 1)")] public float StartAlpha { get; set; } = 0.4f;
                [JsonProperty("End transparency (from 0 to 1)")] public float EndAlpha { get; set; } = 0.1f;
            }

            // Creates the UI interaction settings with default controller line settings.
            public _UIInteraction()
            {
                ControllerLines = new _ControllerLines();
            }
        }

        [JsonProperty("DesktopView Settings")] public _DesktopView DesktopView { get; set; }
        public class _DesktopView
        {
            [JsonProperty("Enabled by default")] public bool Enabled { get; set; } = false;
            [JsonProperty("World view FOV")] public float WorldCamFOV { get; set; } = 90;
            [JsonProperty("UI view FOV")] public float UICamFOV { get; set; } = 90;
        }

        [JsonProperty("Miscellaneous/Unsorted Settings")] public _Misc Misc { get; set; }
        public class _Misc
        {
            // off keeps PortalPatch.FixRed's behaviour: no blood instance buffers and no oil draw
            [JsonProperty("Game blood and oil stains (experimental)")] public bool Stains { get; set; } = false;
        }

        // Replaces sections that the config file set to null with their defaults.
        public void FillMissingSections()
        {
            UKBinds ??= UKBindings.GetBinds();
            VRBinds ??= new _VRBinds();
            Controllers ??= new _Controllers();
            CBS ??= new _CBS();
            MBP ??= new _MBP();
            VRBody ??= new _VRBody();
            UIInteraction ??= new _UIInteraction();
            UIInteraction.ControllerLines ??= new _UIInteraction._ControllerLines();
            DesktopView ??= new _DesktopView();
            Misc ??= new _Misc();
        }

        [JsonProperty("Auto update")] public string AutoUpdate { get; set; } = "Notify"; // Off, Notify or Download; see README "Updates"

        // Creates a config with default settings and the current game binds.
        public NewConfig()
        {
            UKBinds = UKBindings.GetBinds();
            VRBinds = new _VRBinds();
            Controllers = new _Controllers();
            CBS = new _CBS();
            MBP = new _MBP();
            VRBody = new _VRBody();
            UIInteraction = new _UIInteraction();
            DesktopView = new _DesktopView();
            Misc = new _Misc();
        }
    }
}
#pragma warning restore IDE1006 // Naming Styles
