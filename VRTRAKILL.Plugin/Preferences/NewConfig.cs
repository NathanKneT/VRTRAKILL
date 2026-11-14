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
            // stand (or sit) as you play and press it to match your head to the game's eye height again
            [JsonProperty("Recalibrate height")] public string RecalibrateHeight { get; set; } = "H";
        }

        // 1 = the game's own speed. The old default 0.575 also cut jump and wall jump power, so jumps reached about a third
        // of their height (gravity is unchanged) and tutorial jumps were impossible: the most reported gameplay complaint.
        [JsonProperty("Movement multiplier")] public float MovementMultiplier { get; set; } = 1f;
        // Applies to jump and wall jump power only. Below 1 some jumps in the levels cannot be made.
        [JsonProperty("Jump multiplier")] public float JumpMultiplier { get; set; } = 1f;

        // Moves the view and the hands up (positive) or down (negative), in meters. For seated play or a view that sits
        // too low or too high; the log prints the eye height at the first level.
        [JsonProperty("Height offset (meters)")] public float HeightOffset { get; set; } = 0f;
        // true: at the start of each level your head is put at the game's own eye height, whatever your real height and
        // seated or standing; you still crouch and lean for real from there. false: your real height above the floor.
        [JsonProperty("Auto height")] public bool AutoHeight { get; set; } = true;

        // Version of the defaults this file was written with; null for files from before 2.0.0. See Migrate.
        [JsonProperty("Config version")] public int? ConfigVersion { get; set; }
        // 2: new movement and punch defaults. 3: whiplash aim marker and height offset added. 4: auto height. 5: HUD on wrist. 6: "Left handed" renamed (was "Left handed (BROKEN)"). 7: "Windows key presses". 8: rumble settings. 9: comfort vignette. 10: smaller UI. 11: screen flashes. 12: coin throw. 13: low health warning. 14: adaptive triggers. 15: weapon charge shake. 16: parry aim. 17: parry warning. 18: parry aim by the eyes.
        // Bump it when settings are added, so existing files are rewritten and show the new settings.
        public const int CurrentConfigVersion = 18;

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
            if ((ConfigVersion ?? 1) < 10 && UIInteraction != null && System.Math.Abs(UIInteraction.UISize - 0.0625f) < 0.0001f)
                UIInteraction.UISize = new _UIInteraction().UISize;
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
            // scales all controller vibration (the game's rumble and the mod's pulses); 0 to 2
            [JsonProperty("Rumble strength")] public float RumbleStrength { get; set; } = 1f;
            // a tick in the hand that hits, a knock on a kill (Patches/Controllers/PatchHitFeedback)
            [JsonProperty("Hit and kill rumble")] public bool HitRumble { get; set; } = true;
            // a thud in both hands when hurt, stronger with the damage
            [JsonProperty("Damage rumble")] public bool DamageRumble { get; set; } = true;
            // ticks when the menu laser reaches an item and when it clicks
            [JsonProperty("Menu rumble")] public bool MenuRumble { get; set; } = true;
            // a tick in the arm hand when an enemy projectile comes within parry reach (Systems/Feedback/ParryWarning)
            [JsonProperty("Parry warning")] public bool ParryWarning { get; set; } = true;
            // PS VR2 Sense adaptive trigger effects per weapon, through the PSVR2 Toolkit when it is installed (Systems/Feedback/AdaptiveTriggers)
            [JsonProperty("Adaptive triggers")] public bool AdaptiveTriggers { get; set; } = true;
            // guns in the left hand, arm in the right, and the buttons mirrored (Input/LeftHandedBindings)
            [JsonProperty("Left handed")] public bool LeftHanded { get; set; } = false;
            // the 1.0.x name, read so the choice survives the rename; never written back
            [JsonProperty("Left handed (BROKEN)")] private bool LegacyLeftHanded { set { if (value) LeftHanded = true; } }
            [JsonProperty("Finger tracking")] public bool FingerTracking { get; set; } = false;
            // VR buttons as real Windows key presses, like 1.0.x (needs the game window in front); off: a virtual
            // keyboard and mouse inside the game (Input/VirtualInput)
            [JsonProperty("Windows key presses")] public bool WindowsKeyPresses { get; set; } = false;
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
            // a coin tossed while the arm hand makes a throwing move flies along the throw (Patches/ULTRAKILL/PatchRevolver)
            [JsonProperty("Coin follows the throw")] public bool CoinThrow { get; set; } = true;
            // where parried projectiles go: "Eyes" where your eyes look (PS VR2 with the PSVR2 Toolkit; elsewhere, or without
            // a gaze, where the head looks), "Head" where the head looks, like the flat game's camera, "Hand" along the arm
            // hand (Patches/ULTRAKILL/PatchPunch)
            [JsonProperty("Parry aim")] public string ParryAim { get; set; } = "Eyes";
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
            // the menus and the floating HUD, as a scale of a 1920x1080 canvas 72 m away: 0.04 spans about 56 by 33 degrees.
            // The old 0.0625 spanned about 80 degrees, which put the style meter and other edge panels where headset
            // lenses blur ("the UI is too close, I can't read it", PS VR2 test).
            [JsonProperty("UI Size (from 0 to 0.1)")] public float UISize { get; set; } = 0.04f;
            [JsonProperty("Controller-based")] public bool ControllerBased { get; set; } = true;
            // health and stamina on the back of the arm-hand wrist, like a watch; false keeps them floating in the view
            [JsonProperty("HUD on wrist")] public bool WristHud { get; set; } = true;

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

        [JsonProperty("Comfort Settings")] public _Comfort Comfort { get; set; }
        public class _Comfort
        {
            // darkens the edge of the view while moving fast or turning with the stick (Systems/VRCamera/ComfortVignette)
            [JsonProperty("Comfort vignette")] public bool Vignette { get; set; } = false;
            // how dark the edge gets, 0 to 1
            [JsonProperty("Comfort vignette strength")] public float VignetteStrength { get; set; } = .85f;
            // full-view white (parry) and red (damage) flashes: "Reduced" caps how bright they get, "Off" hides them,
            // "Full" leaves them as the game draws them (Systems/UI/FlashLimiter)
            [JsonProperty("Screen flashes")] public string ScreenFlashes { get; set; } = "Reduced";
            // below 30 health: a heartbeat in both hands and a dark red glow at the edge of the view (Systems/Feedback/LowHealth)
            [JsonProperty("Low health warning")] public bool LowHealthWarning { get; set; } = true;
            // how much of the game's shake a charging weapon keeps, 0 (steady) to 1 (the game's); the full shake is a
            // rattle at arm's length in VR (Patches/ULTRAKILL/WeaponShakeP)
            [JsonProperty("Weapon charge shake")] public float WeaponShake { get; set; } = .1f;
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
            Comfort ??= new _Comfort();
            Misc ??= new _Misc();
        }

        [JsonProperty("Auto update")] public string AutoUpdate { get; set; } = "Notify"; // Off, Notify or Download; see docs/UPDATER.md

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
