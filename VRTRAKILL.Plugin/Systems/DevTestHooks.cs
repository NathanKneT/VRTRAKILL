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

        // Whether the test file has this line; read at once, for checks that run before VR starts. "autoflat" makes the
        // "Auto" start act as if SteamVR were not running (flat start and its notice), without closing SteamVR.
        public static bool Requested(string Line)
        {
            try
            {
                string File_ = Path.Combine(PluginInfo.PluginPath, "VRTRAKILL_TEST_SCENE.txt");
                return File.Exists(File_) && Array.Exists(File.ReadAllLines(File_), L => L.Trim().Equals(Line, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception) { return false; }
        }

        public static void OnVRStarted()
        {
            if (Done) return;
            string Scene = Environment.GetEnvironmentVariable("VRTRAKILL_TEST_SCENE");
            try
            {
                string File_ = Path.Combine(PluginInfo.PluginPath, "VRTRAKILL_TEST_SCENE.txt");
                if (string.IsNullOrEmpty(Scene) && File.Exists(File_))
                {
                    string[] Lines = File.ReadAllLines(File_);
                    Scene = Lines.Length > 0 ? Lines[0].Trim() : null;
                    JumpTest = Array.Exists(Lines, L => L.Trim().Equals("jumptest", StringComparison.OrdinalIgnoreCase));
                    PunchTest = Array.Exists(Lines, L => L.Trim().Equals("punchtest", StringComparison.OrdinalIgnoreCase));
                    ShortcutTest = Array.Exists(Lines, L => L.Trim().Equals("shortcuttest", StringComparison.OrdinalIgnoreCase));
                    AimTest = Array.Exists(Lines, L => L.Trim().Equals("aimtest", StringComparison.OrdinalIgnoreCase));
                    WhiplashTest = Array.Exists(Lines, L => L.Trim().Equals("whiplashtest", StringComparison.OrdinalIgnoreCase));
                    WeaponView = Array.Exists(Lines, L => L.Trim().Equals("weaponview", StringComparison.OrdinalIgnoreCase));
                    WristView = Array.Exists(Lines, L => L.Trim().Equals("wristview", StringComparison.OrdinalIgnoreCase));
                    PauseView = Array.Exists(Lines, L => L.Trim().Equals("pauseview", StringComparison.OrdinalIgnoreCase));
                    InputTest = Array.Exists(Lines, L => L.Trim().Equals("inputtest", StringComparison.OrdinalIgnoreCase));
                    EndTest = Array.Exists(Lines, L => L.Trim().Equals("endtest", StringComparison.OrdinalIgnoreCase));
                    HapticTest = Array.Exists(Lines, L => L.Trim().Equals("haptictest", StringComparison.OrdinalIgnoreCase));
                    RangeView = Array.Exists(Lines, L => L.Trim().Equals("rangeview", StringComparison.OrdinalIgnoreCase));
                    PerfTest = Array.Exists(Lines, L => L.Trim().Equals("perftest", StringComparison.OrdinalIgnoreCase));
                    LevelSweep = Array.Exists(Lines, L => L.Trim().Equals("levelsweep", StringComparison.OrdinalIgnoreCase));
                    PauseDump = Array.Exists(Lines, L => L.Trim().Equals("pausedump", StringComparison.OrdinalIgnoreCase));
                    PromptTest = Array.Exists(Lines, L => L.Trim().Equals("prompttest", StringComparison.OrdinalIgnoreCase));
                    VRSettingsTest = Array.Exists(Lines, L => L.Trim().Equals("vrsettingstest", StringComparison.OrdinalIgnoreCase));
                    Unfocused = Array.Exists(Lines, L => L.Trim().Equals("unfocused", StringComparison.OrdinalIgnoreCase));
                    StreamerTest = Array.Exists(Lines, L => L.Trim().Equals("streamertest", StringComparison.OrdinalIgnoreCase));
                    StyleTest = Array.Exists(Lines, L => L.Trim().Equals("styletest", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception e) { Vars.Log.LogWarning("Test scene file unreadable: " + e.Message); }
            if (string.IsNullOrEmpty(Scene)) return;
            Done = true;
            // the plugin filters Unity's exceptions out (filterLogType Warning); a test must see them
            Debug.unityLogger.filterLogType = LogType.Exception;
            Application.logMessageReceived += (Message, Stack, Type) =>
            { if (Type == LogType.Exception) { Exceptions++; Vars.Log.LogError("Test run exception: " + Message + " | " + Stack); } };
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
            if (Unfocused) yield return LoseFocus(Clock);
            if (InputTest) yield return MeasureInput(Clock);
            if (HapticTest) yield return MeasureHaptics(Clock);
            if (RangeView) yield return ShowRange(Clock);
            if (PerfTest) yield return MeasurePerformance(Clock);
            if (StreamerTest) yield return CheckStreamer(Clock);
            if (StyleTest) yield return ShowStyle(Clock);
            if (LevelSweep) yield return SweepLevels(Clock);
            if (PauseDump) yield return DumpPause(Clock);
            if (PromptTest) yield return CheckPrompts(Clock);
            if (VRSettingsTest) yield return CheckVRSettings(Clock);
            if (EndTest) yield return FinishLevel(Clock);
            if (JumpTest) yield return MeasureJumps(Clock);
            if (PunchTest) yield return MeasurePunches(Clock);
            if (ShortcutTest) yield return PressSlideDashPause(Clock);
            if (AimTest) yield return MeasureAim(Clock);
            if (WhiplashTest) yield return MeasureWhiplash(Clock);
            if (WeaponView) yield return ShowWeapons(Clock);
            if (WristView) yield return ShowWrist(Clock);
            if (PauseView) yield return ShowPause(Clock);
        }

        // Where the pause menu is, seen from the left eye: distance and angle off the gaze.
        private static string PauseMenuPlace()
        {
            Camera L = VRCamera.Patches.CameraConverterP.leftEye;
            GameObject PM = MonoSingleton<OptionsManager>.Instance != null ? MonoSingleton<OptionsManager>.Instance.pauseMenu : null;
            if (L == null || PM == null) return "no pause menu";
            Vector3 Dir = PM.transform.position - L.transform.position;
            return $"menu active {PM.activeInHierarchy}, {Dir.magnitude:0.0} m away, {Vector3.Angle(L.transform.forward, Dir):0} deg off the gaze, "
                 + $"UI camera occlusion culling {(UI.UIConverter.UICamera != null && UI.UIConverter.UICamera.useOcclusionCulling)}";
        }

        // A line "pauseview": opens the pause menu (OptionsManager.Pause), logs "Pause view: open" (capture),
        // turns the player 60 degrees with the turn offset like the turn stick, logs "Pause view: turned" (capture),
        // then closes the menu. Shows whether the menu stays put in the world and how the level behind it looks.
        private static bool PauseView;
        private static IEnumerator ShowPause(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            // hands down along the body, out of the view
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = new Pose(new Vector3(-.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = new Pose(new Vector3(.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            // the game's own pause (this checks where the menu goes, not the pause button)
            MonoSingleton<OptionsManager>.Instance.Pause();
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
            Vars.Log.LogWarning($"Pause view: open (paused {Vars.IsPaused}, frozen {Vars.IsPlayerFrozen}; {PauseMenuPlace()})");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            Input.InputVars.TurnOffset += 60f;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            Vars.Log.LogWarning($"Pause view: turned (paused {Vars.IsPaused}, frozen {Vars.IsPlayerFrozen}; {PauseMenuPlace()})");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            Input.InputVars.TurnOffset -= 60f;
            MonoSingleton<OptionsManager>.Instance.UnPause();
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            Vars.Log.LogWarning("Pause view: done.");
        }

        // Places the arm hand and the gun hand, given as poses for a right-handed player. With "Left handed" the arm hand
        // is the right controller, so both poses are mirrored across the middle of the body.
        private static void PlaceHands(Pose ArmHand, Pose GunHand)
        {
            bool Left = Vars.Config.Controllers.LeftHanded;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = Left ? Mirror(GunHand) : ArmHand;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = Left ? Mirror(ArmHand) : GunHand;
        }
        private static Pose Mirror(Pose P)
            => new Pose(new Vector3(-P.position.x, P.position.y, P.position.z), new Quaternion(P.rotation.x, -P.rotation.y, -P.rotation.z, P.rotation.w));

        // A line "wristview": raises the arm hand in front of the face like looking at a watch, with the gun hand lowered,
        // and logs "Wrist view: ready" for 6 s so a script can capture the headset window (the wrist HUD).
        private static bool WristView;
        private static IEnumerator ShowWrist(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            PlaceHands(new Pose(new Vector3(.02f, -.12f, .46f), Quaternion.Euler(-15, 75, 0)), new Pose(new Vector3(.25f, -.45f, .25f), Quaternion.Euler(-45, 0, 0)));
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            UI.WristHud Hud = UnityEngine.Object.FindObjectOfType<UI.WristHud>();
            Controllers.VRArmsSystem Arms = Vars.Registered<Controllers.VRArmsSystem>();
            Canvas HudCanvas = Hud != null ? Hud.GetComponent<Canvas>() : null;
            Vars.Log.LogWarning("Wrist view: ready" + (Hud != null && Arms != null
                ? $" (panel {Vector3.Distance(Hud.transform.position, Arms.transform.position):0.00} m from the arm controller, canvas shown {(HudCanvas != null && HudCanvas.enabled)}, scale {Hud.transform.lossyScale.x:0.00000})"
                : " (no wrist HUD)"));
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 6) yield return null;
            Vars.Log.LogWarning("Wrist view: done.");
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
        }

        // A line "weaponview": puts both hands at a natural holding pose in front of the body (tracking space, head at
        // the origin like SteamVR's null headset) and shows each weapon family for 4 s, logging "Weapon view: <name>"
        // so a script can capture the headset window. For checking where the models sit in the hand.
        private static bool WeaponView;
        private static IEnumerator ShowWeapons(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            GunControl Guns = null;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Guns == null || Guns.allWeapons.Count == 0)) { Guns = Vars.Registered<GunControl>(); yield return null; }
            if (Guns == null) { Vars.Log.LogWarning("Weapon view: no guns."); yield break; }
            // controller pitched up 45 degrees: the gun offset (45 degrees down from the controller) then aims level
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = BesideHead(new Vector3(.22f, -.38f, .38f), Quaternion.Euler(-45, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = BesideHead(new Vector3(-.22f, -.38f, .38f), Quaternion.Euler(-45, 0, 0));
            var Shown = new System.Collections.Generic.HashSet<string>();
            foreach (GameObject Weapon in new System.Collections.Generic.List<GameObject>(Guns.allWeapons))
            {
                if (Weapon == null) continue;
                string Family = Weapon.name.Split(' ')[0];
                if (!Shown.Add(Family)) continue;
                Guns.ForceWeapon(Weapon);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
                Vars.Log.LogWarning("Weapon view: " + Weapon.name);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            }
            Vars.Log.LogWarning("Weapon view: done.");
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
        }

        // A pose in tracking space at this offset from the headset, turned with its heading: with a real headset the head
        // is not at the tracking origin (the null driver's is), and hands placed from the origin were out of view.
        private static Pose BesideHead(Vector3 Offset, Quaternion Turn)
        {
            UnityEngine.XR.InputDevice Head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if (!Head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 At)
                || !Head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion Facing))
                return new Pose(Offset, Turn);
            Quaternion Heading = Quaternion.Euler(0f, Facing.eulerAngles.y, 0f);
            return new Pose(At + Heading * Offset, Heading * Turn);
        }

        // A line "whiplashtest": checks the whiplash aim marker (visible, on the arm hand's aim line) and throws the
        // whiplash through the mod's SteamVR whiplash handler, then compares the game's throw direction with that aim.
        private static bool WhiplashTest;
        private static IEnumerator MeasureWhiplash(System.Diagnostics.Stopwatch Clock)
        {
            // wait for the level: the main menu's objects are still registered for a moment after LoadScene
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 5) yield return null;
            HookArm Hook = null;
            while (Clock.Elapsed.TotalSeconds < T + 35 && (Hook == null || !Hook.equipped)) { Hook = Vars.Registered<HookArm>(); yield return null; }
            if (Hook == null || !Hook.equipped) { Vars.Log.LogWarning("Whiplash test: no equipped whiplash in this save or level."); yield break; }
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1) yield return null;
            Hook = Vars.Registered<HookArm>();
            if (Hook == null) { Vars.Log.LogWarning("Whiplash test: the whiplash went away."); yield break; }
            Transform Aim = Vars.Registered<Controllers.VRArmsSystem>()?.GunOffset?.transform;
            UI.WhiplashAimMarker Marker = Hook.GetComponentInChildren<UI.WhiplashAimMarker>(true);
            LineRenderer LR = Marker != null ? Marker.GetComponent<LineRenderer>() : null;
            if (Aim == null || LR == null) { Vars.Log.LogWarning($"Whiplash test: arm hand {Aim != null}, marker {LR != null}."); yield break; }
            Vector3 Center = Vector3.zero; for (int i = 0; i < LR.positionCount; i++) Center += LR.GetPosition(i); Center /= Mathf.Max(1, LR.positionCount);
            Vars.Log.LogWarning($"Whiplash test: marker shown {LR.enabled}, {Vector3.Angle(Center - Aim.position, Aim.forward):0.00} deg off the arm hand's aim, "
                              + $"{Vector3.Distance(Center, Aim.position):0.00} m ahead (crosshair distance {Vars.Config.CBS.CrosshairDistance}).");
            Vector3 AimAtThrow = Aim.forward;
            Input.SVRActionsManager.TestButton("whiplash", true);
            T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 1 && Hook.state == HookState.Ready) yield return null;
            Input.SVRActionsManager.TestButton("whiplash", false);
            bool AutoAimed = MonoSingleton<CameraFrustumTargeter>.Instance != null && MonoSingleton<CameraFrustumTargeter>.Instance.CurrentTarget && MonoSingleton<CameraFrustumTargeter>.Instance.IsAutoAimed;
            Vars.Log.LogWarning($"Whiplash test: state {Hook.state}, throw direction {Vector3.Angle(Hook.throwDirection, AimAtThrow):0.00} deg off the arm hand's aim (game auto-aim on a target: {AutoAimed}), marker shown while throwing {LR.enabled}.");
        }

        // A line "aimtest": every gun fires from the gun hand along its aim (the shot code's camera reads are redirected
        // to the hand). For each weapon the save has, measures the angle between the crosshair and that shot line, and,
        // with the rocket launcher, fires a rocket and measures its start point and direction against the hand.
        private static bool AimTest;
        private static IEnumerator MeasureAim(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            GunControl Guns = Vars.Registered<GunControl>();
            // wait for the level to hand out weapons
            while (Clock.Elapsed.TotalSeconds < T + 30 && (Guns == null || Guns.allWeapons.Count == 0)) { Guns = Vars.Registered<GunControl>(); yield return null; }
            if (Guns == null) { Vars.Log.LogWarning("Aim test: no GunControl."); yield break; }
            // the level's crosshair may not exist yet; a CrosshairController of our own runs the same placement code
            UI.CrosshairController Cross = new GameObject("VRTRAKILL aim test crosshair").AddComponent<UI.CrosshairController>();
            Vars.Log.LogWarning($"Aim test: {Guns.allWeapons.Count} weapon(s) in the save.");
            foreach (GameObject Weapon in new System.Collections.Generic.List<GameObject>(Guns.allWeapons))
            {
                if (Weapon == null) continue;
                Guns.ForceWeapon(Weapon);
                T = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < T + 0.8) yield return null;
                Cross.LateUpdate();
                Transform Hand = Vars.DominantHand.transform;
                float CrossError = Vector3.Angle(Cross.transform.position - Hand.position, Hand.forward);
                string Line = $"Aim test, {Weapon.name}: crosshair {CrossError:0.00} deg off the shot line, model origin {Vector3.Distance(Weapon.transform.position, Hand.position):0.00} m from the hand";
                RocketLauncher RL = Weapon.GetComponent<RocketLauncher>();
                if (RL != null)
                {
                    var Before = new System.Collections.Generic.HashSet<Grenade>(UnityEngine.Object.FindObjectsOfType<Grenade>());
                    RL.Shoot();
                    yield return null;
                    foreach (Grenade G in UnityEngine.Object.FindObjectsOfType<Grenade>())
                        if (!Before.Contains(G))
                        {
                            Line += $"; rocket fired {Vector3.Distance(G.transform.position, Hand.position):0.00} m from the hand, {Vector3.Angle(G.transform.forward, Hand.forward):0.00} deg off the hand's aim, "
                                  + $"{Vector3.Angle(G.transform.forward, Cross.transform.position - G.transform.position):0.00} deg off the crosshair";
                            break;
                        }
                }
                Vars.Log.LogWarning(Line + ".");
            }
        }

        // A line "shortcuttest": holds slide and dash, then presses pause, through the mod's SteamVR handlers, like a
        // player double-tapping pause mid-slide-dash. With the default binds this used to send Ctrl+Shift+Esc to Windows
        // (Task Manager, issue #117). The test script checks whether Task Manager opened.
        private static bool ShortcutTest;
        private static IEnumerator PressSlideDashPause(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
            Vars.Log.LogWarning($"Shortcut test: slide key {Prefs.ConfigMaster.KSlide}, dash key {Prefs.ConfigMaster.KDash}; holding both, then pause (window focused {Application.isFocused}).");
            Input.SVRActionsManager.TestButton("slide", true);
            Input.SVRActionsManager.TestButton("dash", true);
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 0.2) yield return null;
            Input.SVRActionsManager.TestButton("escape", true);
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 0.2) yield return null;
            Input.SVRActionsManager.TestButton("escape", false);
            Input.SVRActionsManager.TestButton("slide", false);
            Input.SVRActionsManager.TestButton("dash", false);
            Vars.Log.LogWarning("Shortcut test: done.");
        }

        // A line "punchtest": presses the punch button through the mod's own SteamVR handler (SVRActionsManager) with a
        // simulated hand speed, and counts the punches the game starts (Punch.PunchStart). The game window must have
        // focus, because the button is a Windows key press.
        private static bool PunchTest;
        private static int Punches;
        private static void CountPunch() => Punches++;
        private static IEnumerator MeasurePunches(System.Diagnostics.Stopwatch Clock)
        {
            new HarmonyLib.Harmony("vrtrakill.devtest").Patch(HarmonyLib.AccessTools.Method(typeof(Punch), nameof(Punch.PunchStart)),
                postfix: new HarmonyLib.HarmonyMethod(typeof(DevTestHooks), nameof(CountPunch)));
            bool MBP = Vars.Config.EnableMBP;
            var Cases = new (string Name, bool Mbp, float Speed, int Expected)[]
            {
                ("swing punching on, hand still", true, 0f, 0),
                ("swing punching on, 1.5 m/s swing", true, 1.5f, 0),
                ("swing punching on, 2.5 m/s swing", true, 2.5f, 1),
                ("swing punching off (button only), hand still", false, 0f, 1),
            };
            foreach (var C in Cases)
            {
                double T = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < T + 2.5) yield return null; // fist cooldown and stamina
                Vars.Config.EnableMBP = C.Mbp;
                Controllers.VRArmsSystem.TestHandSpeed = C.Speed;
                int Before = Punches;
                bool Locked = GameStateManager.Instance != null && GameStateManager.Instance.PlayerInputLocked;
                Input.SVRActionsManager.TestPunchButton(true);
                T = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < T + 0.3) yield return null;
                Input.SVRActionsManager.TestPunchButton(false);
                T = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < T + 0.5) yield return null;
                int Got = Punches - Before;
                Vars.Log.LogWarning($"Punch test, {C.Name}: {Got} punch(es), expected {C.Expected} -> {(Got == C.Expected ? "OK" : "DIFFERENT")}"
                                  + $" (window focused {Application.isFocused}, input locked {Locked}).");
            }
            Vars.Config.EnableMBP = MBP;
            Controllers.VRArmsSystem.TestHandSpeed = null;
        }

        // A line "unfocused": once the level is up, puts the Windows taskbar in front, so the checks after it run with
        // the game window unfocused, as when another window is clicked while playing.
        private static bool Unfocused;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr FindWindow(string ClassName, string WindowName);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr Window);
        private static IEnumerator LoseFocus(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            SetForegroundWindow(FindWindow("Shell_TrayWnd", null));
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            Vars.Log.LogWarning($"Focus: game window focused {Application.isFocused} (virtual input {Input.SVRActionsManager.UseVirtualInput}).");
        }

        // A line "inputtest": presses buttons through the mod's SteamVR handlers, as the controllers do, and checks the game
        // reacted: weapon slot keys, the wheel (next weapon), shoot (the revolver's Shoot runs), jump (NewMovement.Jump
        // runs) and pause then unpause. Logs "Input test, <what>: ... -> OK" or "-> DIFFERENT".
        private static bool InputTest;
        private static int Shots, Jumps;
        private static void CountShot() => Shots++;
        private static void CountJump() => Jumps++;
        private static IEnumerator Press(System.Diagnostics.Stopwatch Clock, string Button, double After)
        {
            Input.SVRActionsManager.TestButton(Button, true);
            double T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .15) yield return null;
            Input.SVRActionsManager.TestButton(Button, false);
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + After) yield return null;
        }
        private static void Report(string What, bool Ok, string Detail)
            => Vars.Log.LogWarning($"Input test, {What}: {Detail} -> {(Ok ? "OK" : "DIFFERENT")} (window focused {Application.isFocused}, virtual input {Input.SVRActionsManager.UseVirtualInput}).");
        private static IEnumerator MeasureInput(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            var H = new HarmonyLib.Harmony("vrtrakill.devtest.input");
            H.Patch(HarmonyLib.AccessTools.Method(typeof(Revolver), "Shoot"), postfix: new HarmonyLib.HarmonyMethod(typeof(DevTestHooks), nameof(CountShot)));
            H.Patch(HarmonyLib.AccessTools.Method(typeof(NewMovement), nameof(NewMovement.Jump)), postfix: new HarmonyLib.HarmonyMethod(typeof(DevTestHooks), nameof(CountJump)));
            GunControl GC = MonoSingleton<GunControl>.Instance;
            if (GC == null) { Vars.Log.LogWarning("Input test: no GunControl in this level (use Level 1-1)."); yield break; }

            yield return Press(Clock, "slot1", 1.2);
            GameObject W1 = GC.currentWeapon; int S1 = GC.currentSlotIndex;
            yield return Press(Clock, "slot2", 1.2);
            Report("weapon slot 2", GC.currentSlotIndex == 2 && GC.currentWeapon != W1, $"slot {S1} -> {GC.currentSlotIndex}");

            GameObject W2 = GC.currentWeapon; int S2 = GC.currentSlotIndex;
            yield return Press(Clock, "next", 1.2);
            Report("next weapon (wheel)", GC.currentWeapon != W2, $"slot {S2} -> {GC.currentSlotIndex}, weapon {(GC.currentWeapon != W2 ? "changed" : "same")}, scroll switching {MonoSingleton<InputManager>.Instance.ScrOn}");

            yield return Press(Clock, "slot1", 1.5);
            int ShotsBefore = Shots;
            yield return Press(Clock, "shoot", .8);
            Report("shoot (revolver)", Shots > ShotsBefore, $"{Shots - ShotsBefore} shot(s)");

            NewMovement NM = Vars.Registered<NewMovement>();
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 5 && NM != null && !NM.gc.onGround) yield return null;
            int JumpsBefore = Jumps;
            yield return Press(Clock, "jump", .8);
            Report("jump", Jumps > JumpsBefore, $"{Jumps - JumpsBefore} jump(s)");

            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            bool PausedBefore = Vars.IsPaused;
            yield return Press(Clock, "escape", 1.2);
            bool PausedAfter = Vars.IsPaused;
            Report("pause", !PausedBefore && PausedAfter, $"paused {PausedBefore} -> {PausedAfter}");
            yield return Press(Clock, "escape", 1.2);
            Report("unpause", PausedAfter && !Vars.IsPaused, $"paused {PausedAfter} -> {Vars.IsPaused}");
            H.UnpatchSelf();
            Vars.Log.LogWarning("Input test: done.");
        }

        // A line "endtest": finishes the level the way a player does, by putting the player in the final pit, then logs
        // "End test: rank" when the rank screen is up, presses shoot through the mod's handler to go on, and logs
        // "End test: next" once the next level runs (each one a capture point). Checks the end of level and the level
        // change in VR (rank screen, "endless elevator" reports). The run is marked "major assists used" first, which
        // the game checks before submitting a score: nothing goes to the Steam leaderboards (the test stops if the game
        // still says it could submit). The game saves the rank and progress, so the test copies the Saves folder first
        // and puts it back while the game still runs: Steam Cloud syncs at exit, and a folder restored after exit got
        // the test's files back from the cloud.
        private static bool EndTest;
        private static string SavesPath => Path.Combine(PluginInfo.GamePath, "Saves");
        private static string SavesCopy => Path.Combine(PluginInfo.PluginPath, "test_saves_copy");
        private static void CopyTree(string From, string To)
        {
            foreach (string F in Directory.GetFiles(From, "*", SearchOption.AllDirectories))
            {
                string Target = Path.Combine(To, F.Substring(From.Length).TrimStart(Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(Target));
                File.Copy(F, Target, true);
                File.SetLastWriteTimeUtc(Target, File.GetLastWriteTimeUtc(F));
            }
        }
        // Puts the Saves folder back as it was copied: files the test added are deleted, changed ones copied back.
        private static int RestoreSaves()
        {
            int Changed = 0;
            foreach (string F in Directory.GetFiles(SavesPath, "*", SearchOption.AllDirectories))
                if (!File.Exists(Path.Combine(SavesCopy, F.Substring(SavesPath.Length).TrimStart(Path.DirectorySeparatorChar)))) { File.Delete(F); Changed++; }
            foreach (string F in Directory.GetFiles(SavesCopy, "*", SearchOption.AllDirectories))
            {
                string Target = Path.Combine(SavesPath, F.Substring(SavesCopy.Length).TrimStart(Path.DirectorySeparatorChar));
                if (File.Exists(Target) && new FileInfo(Target).Length == new FileInfo(F).Length && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(Target), File.ReadAllBytes(F))) continue;
                File.Copy(F, Target, true); File.SetLastWriteTimeUtc(Target, File.GetLastWriteTimeUtc(F)); Changed++;
            }
            Directory.Delete(SavesCopy, true);
            return Changed;
        }
        private static string RankCanvasPlace(Component Fr)
        {
            Camera L = VRCamera.Patches.CameraConverterP.leftEye;
            Canvas C = Fr != null ? Fr.GetComponentInParent<Canvas>() : null;
            if (C != null) C = C.rootCanvas;
            if (L == null || C == null) return "no canvas";
            Vector3 Dir = Fr.transform.position - L.transform.position;
            return $"canvas {C.name} {C.renderMode}, layer {LayerMask.LayerToName(C.gameObject.layer)}, {Dir.magnitude:0.0} m away, {Vector3.Angle(L.transform.forward, Dir):0} deg off the gaze";
        }
        private static IEnumerator FinishLevel(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            StatsManager SM = MonoSingleton<StatsManager>.Instance;
            NewMovement NM = Vars.Registered<NewMovement>();
            if (SM == null || NM == null) { Vars.Log.LogWarning("End test: no stats manager or player. End test: done."); yield break; }
            SM.majorUsed = true;
            if (LeaderboardController.CanSubmitScores) { Vars.Log.LogWarning("End test: the game could still submit a score, stopped. End test: done."); yield break; }
            if (Directory.Exists(SavesCopy)) Directory.Delete(SavesCopy, true);
            CopyTree(SavesPath, SavesCopy);
            // the final room is often switched off until the player gets there: find the pit anyway and switch its room on
            FinalPit Pit = null;
            foreach (FinalPit P in Resources.FindObjectsOfTypeAll<FinalPit>())
                if (P.gameObject.scene.isLoaded && !P.fakeEnd && !P.secondPit && !P.rankless) { Pit = P; break; }
            if (Pit == null) { Vars.Log.LogWarning("End test: no final pit in this level. End test: done."); yield break; }
            int Switched = 0;
            for (Transform A = Pit.transform; A != null; A = A.parent)
                if (!A.gameObject.activeSelf) { A.gameObject.SetActive(true); Switched++; }
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1) yield return null;
            Collider PitCollider = Pit.GetComponent<Collider>();
            if (PitCollider == null) { Vars.Log.LogWarning("End test: the final pit has no collider. End test: done."); yield break; }
            Vars.Log.LogWarning($"End test: found the final pit {Pit.name} ({Switched} switched-off object(s) turned on).");
            string Scene = SceneHelper.CurrentScene;
            NM.rb.velocity = Vector3.zero;
            NM.rb.position = PitCollider.bounds.center;
            NM.transform.position = PitCollider.bounds.center;
            Vars.Log.LogWarning($"End test: pit (scene {Scene}, leaderboard submit possible {LeaderboardController.CanSubmitScores}).");

            T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 20 && (SM.fr == null || !SM.fr.gameObject.activeInHierarchy)) yield return null;
            if (SM.fr == null || !SM.fr.gameObject.activeInHierarchy) { Vars.Log.LogWarning($"End test: no rank screen after 20 s (level over {NM.levelOver}). End test: done."); yield break; }
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            Vars.Log.LogWarning($"End test: rank ({RankCanvasPlace(SM.fr)}).");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;

            // the first press finishes the rank screen; once the player reached the second pit, the next one loads the next level
            double Start = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < Start + 40 && SceneHelper.CurrentScene == Scene)
            {
                yield return Press(Clock, "shoot", 2);
            }
            if (SceneHelper.CurrentScene == Scene) { Vars.Log.LogWarning($"End test: still in {Scene} after 40 s (second pit reached {SM.fr != null && SM.fr.reachedSecondPit}, player y {NM.transform.position.y:0}). End test: done."); yield break; }
            T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 6) yield return null;
            NewMovement Next = Vars.Registered<NewMovement>();
            Vars.Log.LogWarning($"End test: next (scene {SceneHelper.CurrentScene}, player {(Next != null ? "there" : "missing")}, VR eyes {VRCamera.Patches.CameraConverterP.leftEye != null}).");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            Vars.Log.LogWarning($"End test: saves put back as they were ({RestoreSaves()} file(s) restored or removed). End test: done.");
        }

        // A line "haptictest": records the vibration the mod sends (the null headset has no controllers to feel it) and
        // checks: the game's gunfire and slide rumble reach both hands at once; a revolver hit ticks the gun hand, a punch
        // hit the arm hand, a kill knocks harder; getting hurt thuds both hands; sweeping the menu laser down the pause
        // menu ticks once per item. Logs "Haptic test, <what>: ... -> OK" or "-> DIFFERENT".
        private static bool HapticTest;
        private static void HapticReport(string What, bool Ok, string Detail)
            => Vars.Log.LogWarning($"Haptic test, {What}: {Detail} -> {(Ok ? "OK" : "DIFFERENT")}");
        private static IEnumerator MeasureHaptics(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            Feedback.Haptics.Recording = true;
            string Gun = Feedback.Haptics.ArmIsLeft ? "right" : "left", Arm = Feedback.Haptics.ArmIsLeft ? "left" : "right";

            RumbleManager RM = MonoSingleton<RumbleManager>.Instance;
            RM.SetVibration(RumbleProperties.GunFire);
            RM.SetVibration(RumbleProperties.Slide);
            yield return null; yield return null; yield return null;
            float L = Feedback.Haptics.LastLeft, R = Feedback.Haptics.LastRight;
            HapticReport("gunfire + slide rumble", L > 0f && R > 0f, $"left {L:0.00}, right {R:0.00}");
            RM.StopVibration(RumbleProperties.Slide);
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .5) yield return null;

            EnemyIdentifier E = null;
            foreach (EnemyIdentifier C in UnityEngine.Object.FindObjectsOfType<EnemyIdentifier>()) if (!C.dead) { E = C; break; }
            if (E == null)
                foreach (EnemyIdentifier C in Resources.FindObjectsOfTypeAll<EnemyIdentifier>())
                    if (C.gameObject.scene.isLoaded && !C.dead)
                    {
                        for (Transform A = C.transform; A != null; A = A.parent) if (!A.gameObject.activeSelf) A.gameObject.SetActive(true);
                        E = C; break;
                    }
            if (E == null) HapticReport("hits", false, "no enemy in this level");
            else
            {
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1) yield return null;
                Feedback.Haptics.TestLog.Clear();
                E.hitter = "revolver"; E.DeliverDamage(E.gameObject, Vector3.zero, E.transform.position, .01f, false);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .1) yield return null;
                E.hitter = "punch"; E.DeliverDamage(E.gameObject, Vector3.zero, E.transform.position, .01f, false);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .1) yield return null;
                E.hitter = "revolver"; E.DeliverDamage(E.gameObject, Vector3.zero, E.transform.position, 1000f, false);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .1) yield return null;
                string Log = string.Join("; ", Feedback.Haptics.TestLog);
                bool Ok = Log.Contains("hit revolver " + Gun) && Log.Contains("hit punch " + Arm) && Log.Contains("kill revolver " + Gun);
                HapticReport($"hits on {E.enemyType}", Ok, Log);
            }

            NewMovement NM = Vars.Registered<NewMovement>();
            Feedback.Haptics.TestLog.Clear();
            NM.GetHurt(10, false);
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .2) yield return null;
            string Hurt = string.Join("; ", Feedback.Haptics.TestLog);
            HapticReport("hurt", Hurt.Contains("hurt 10 both"), Hurt);
            NM.hp = 100;

            // pause, then aim the laser (it starts at the gun hand's muzzle, 45 degrees below the controller) at each
            // pause menu button in turn: one tick per button
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            Valve.VR.SteamVR_Input_Sources GunSource = Feedback.Haptics.ArmIsLeft ? Valve.VR.SteamVR_Input_Sources.RightHand : Valve.VR.SteamVR_Input_Sources.LeftHand;
            Pose Rest = new Pose(new Vector3(Feedback.Haptics.ArmIsLeft ? .08f : -.08f, -.08f, .3f), Quaternion.identity);
            Pose Down = new Pose(new Vector3(Feedback.Haptics.ArmIsLeft ? -.25f : .25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            if (Feedback.Haptics.ArmIsLeft) { VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = Rest; VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = Down; }
            else { VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = Rest; VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = Down; }
            MonoSingleton<OptionsManager>.Instance.Pause();
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            Controllers.VRControllersSystem Hand = Controllers.VRControllersSystem.Get(GunSource);
            GameObject Menu = MonoSingleton<OptionsManager>.Instance.pauseMenu;
            if (Hand == null || Menu == null) HapticReport("menu laser", false, $"gun hand {Hand != null}, pause menu {Menu != null}");
            else
            {
                Quaternion Rig = Hand.transform.rotation * Quaternion.Inverse(Rest.rotation);
                Quaternion Off = Quaternion.Inverse(Hand.transform.rotation) * Hand.GunOffset.transform.rotation;
                Feedback.Haptics.TestLog.Clear();
                int Aimed = 0;
                foreach (UnityEngine.UI.Selectable B in Menu.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                {
                    if (!B.IsInteractable()) continue;
                    RectTransform RT = (RectTransform)B.transform;
                    Vector3 Target = RT.TransformPoint(RT.rect.center);
                    Quaternion Want = Quaternion.LookRotation(Target - Hand.GunOffset.transform.position, Rig * Vector3.up) * Quaternion.Inverse(Off);
                    Pose P = new Pose(Rest.position, Quaternion.Inverse(Rig) * Want);
                    if (Feedback.Haptics.ArmIsLeft) VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = P;
                    else VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = P;
                    Aimed++;
                    T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .35) yield return null;
                }
                int Ticks = Feedback.Haptics.TestLog.FindAll(S => S.StartsWith("menu hover " + Gun)).Count;
                HapticReport("menu laser", Ticks >= 2 && Ticks >= Aimed - 1, $"aimed at {Aimed} button(s), {Ticks} hover tick(s) on the {Gun} hand: {string.Join("; ", Feedback.Haptics.TestLog)}");
            }
            MonoSingleton<OptionsManager>.Instance.UnPause();
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            Feedback.Haptics.Recording = false;
            Vars.Log.LogWarning("Haptic test: done.");
        }

        // A line "rangeview": logs how far the eyes draw compared with the game's camera (near and far planes, clear
        // flags, fog), then turns the view to four directions with the hands lowered, logging "Range view: <direction>"
        // for a capture each. For checking big levels: is the far scenery drawn, cut off, or flickering?
        private static bool RangeView;
        private static IEnumerator ShowRange(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 5) yield return null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = new Pose(new Vector3(-.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = new Pose(new Vector3(.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            Camera Main = MonoSingleton<CameraController>.Instance != null ? MonoSingleton<CameraController>.Instance.cam : null;
            Camera L = VRCamera.Patches.CameraConverterP.leftEye;
            if (Main != null && L != null)
                Vars.Log.LogWarning($"Range: scene {SceneHelper.CurrentScene}; game camera near {Main.nearClipPlane} far {Main.farClipPlane} clear {Main.clearFlags}; "
                                  + $"left eye near {L.nearClipPlane} far {L.farClipPlane} clear {L.clearFlags} mask same {L.cullingMask == Main.cullingMask}; "
                                  + $"fog {RenderSettings.fog} {RenderSettings.fogMode} {RenderSettings.fogStartDistance:0}-{RenderSettings.fogEndDistance:0} density {RenderSettings.fogDensity:0.0000}; "
                                  + $"depth buffer {(L.targetTexture != null ? L.targetTexture.depth.ToString() : "XR")}, sky enablers {Resources.FindObjectsOfTypeAll<SkyboxEnabler>().Length}");
            // every level starts in the same small room: measure the whole level, then look across it from a high corner
            Bounds B = default; bool Any = false;
            foreach (Renderer R in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!(R is MeshRenderer) || !R.enabled || R.gameObject.layer == (int)Layers.UI) continue;
                if (R.bounds.size.magnitude > 20000f) continue; // sky domes
                if (!Any) { B = R.bounds; Any = true; } else B.Encapsulate(R.bounds);
            }
            NewMovement NM = Vars.Registered<NewMovement>();
            if (Any && Main != null && NM != null)
            {
                float Diagonal = B.size.magnitude;
                Vars.Log.LogWarning($"Range: level geometry {B.size.x:0} x {B.size.y:0} x {B.size.z:0} m, diagonal {Diagonal:0} m; the eyes draw to {L.farClipPlane:0} m -> {(Diagonal < L.farClipPlane ? "the whole level fits" : "parts can be cut off")}.");
                foreach (OutOfBounds O in UnityEngine.Object.FindObjectsOfType<OutOfBounds>()) O.gameObject.SetActive(false);
                foreach (DeathZone Z in UnityEngine.Object.FindObjectsOfType<DeathZone>()) Z.gameObject.SetActive(false);
                Vector3 Corner = B.center + new Vector3(-B.extents.x * .8f, B.extents.y * .5f, -B.extents.z * .8f);
                NM.rb.isKinematic = true;
                NM.rb.position = Corner; NM.transform.position = Corner;
                Vector3 ToCenter = B.center - Corner;
                Input.InputVars.TurnOffset = Mathf.Atan2(ToCenter.x, ToCenter.z) * Mathf.Rad2Deg;
            }
            string[] Names = { "across", "right", "behind", "left" };
            for (int i = 0; i < 4; i++)
            {
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
                Vars.Log.LogWarning($"Range view: {Names[i]}");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
                Input.InputVars.TurnOffset += 90f;
            }
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            Vars.Log.LogWarning("Range view: done.");
        }

        // A line "streamertest": in the level, records 8 s of frame times and SteamVR's dropped and reprojected frames
        // with the streamer view off, then 8 s with it on; saves the streamer picture as a BMP file in the BepInEx folder,
        // logs its mean brightness and colour count (a black or single-colour picture means nothing was drawn) and how
        // often the monitor canvas showed it. In VR, reading the screen back returns an eye, not the window, so the window itself
        // (with the HUD and overlays) is checked with a desktop screenshot. The streamer view setting is put back.
        private static bool StreamerTest;

        // A line "styletest": in the level, starts a style combo and adds a few bonuses half a second apart (KILL,
        // HEADSHOT, DOUBLE KILL, PARRY), so the streamer view's rank, meter and bonus list show without a fight; logs
        // "Style test: shown" (capture the window then) and what the game's panel says. The level is not finished, so
        // nothing is saved.
        private static bool StyleTest;
        private static IEnumerator ShowStyle(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 4) yield return null;
            StyleHUD Style = Vars.Registered<StyleHUD>();
            if (Style == null) { Vars.Log.LogWarning("Style test: no style HUD. Style test: done."); yield break; }
            Style.ComboStart();
            string[] Ids = { "ultrakill.kill", "ultrakill.headshot", "ultrakill.doublekill", "ultrakill.parry" };
            for (int i = 0; i < 4; i++)
            {
                Style.AddPoints(60, Ids[i]);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .5) yield return null;
            }
            string Bonuses = Style.styleInfo != null ? Style.styleInfo.text.Replace('\n', '|') : "none";
            string Fresh = Style.freshnessSliderText != null ? Style.freshnessSliderText.text : "none";
            WeaponHUD W = Vars.Registered<WeaponHUD>();
            GunControl Guns = Vars.Registered<GunControl>();
            UnityEngine.UI.Image Icon = W != null ? W.GetComponent<UnityEngine.UI.Image>() : null;
            Vars.Log.LogWarning($"Style test: shown (rank {Style.rankIndex}, combo {Style.comboActive}, bonuses \"{Bonuses}\", freshness \"{Fresh}\"; "
                + $"weapon HUD {(W != null ? W.name + " active " + W.gameObject.activeInHierarchy : "none")}, icon {(Icon != null && Icon.sprite != null ? Icon.sprite.name : "none")}, "
                + $"guns {(Guns != null ? "no weapons " + Guns.noWeapons + ", current " + (Guns.currentWeapon != null ? Guns.currentWeapon.name : "none") : "none")}).");
            // bonuses leave the game's list after a few seconds: keep adding them while the window is captured
            for (int i = 0; i < 16; i++)
            {
                Style.AddPoints(10, Ids[i % 4]);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .5) yield return null;
            }
            // then the shotgun (slot 2), if the player has one: its icon replaces the revolver's, and its first start logs
            // its parts when the game's arm is not found (PatchRemoveArms)
            if (Guns != null && Guns.slots.Count >= 2 && Guns.slots[1].Count > 0)
            {
                Guns.SwitchWeapon(2);
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
                Vars.Log.LogWarning($"Style test: shotgun out (current {(Guns.currentWeapon != null ? Guns.currentWeapon.name : "none")}, icon {(Icon != null && Icon.sprite != null ? Icon.sprite.name : "none")}).");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 6) yield return null;
            }
            Vars.Log.LogWarning("Style test: done.");
        }

        private static IEnumerator CheckStreamer(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 5) yield return null;
            bool Was = Vars.Config.StreamerView.On;
            Vars.Config.StreamerView.On = false;
            yield return StreamerFrames(Clock, "off");
            Vars.Config.StreamerView.On = true;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
            int Draws = VRCamera.StreamerOverlay.Shown;
            yield return StreamerFrames(Clock, "on");
            Vars.Log.LogWarning($"Streamer test: the monitor showed the picture {VRCamera.StreamerOverlay.Shown - Draws} times in 8 s; camera at {(VRCamera.StreamerView.WorldCamera != null ? VRCamera.StreamerView.WorldCamera.transform.position.ToString() : "none")}.");

            RenderTexture Pic = VRCamera.StreamerView.Picture;
            if (Pic == null) Vars.Log.LogWarning("Streamer test: no picture.");
            else
            {
                RenderTexture Active = RenderTexture.active;
                RenderTexture.active = Pic;
                Texture2D Tex = new Texture2D(Pic.width, Pic.height, TextureFormat.RGB24, false);
                Tex.ReadPixels(new Rect(0, 0, Pic.width, Pic.height), 0, 0); Tex.Apply();
                RenderTexture.active = Active;
                SaveForTest(Tex, "VRTRAKILL_streamer_picture.bmp");
                UnityEngine.Object.Destroy(Tex);
            }
            Vars.Config.StreamerView.On = Was;
            Vars.Log.LogWarning("Streamer test: done.");
        }

        private static IEnumerator StreamerFrames(System.Diagnostics.Stopwatch Clock, string Label)
        {
            Valve.VR.Compositor_CumulativeStats Before = new Valve.VR.Compositor_CumulativeStats(), After = new Valve.VR.Compositor_CumulativeStats();
            uint Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Valve.VR.Compositor_CumulativeStats));
            Valve.VR.OpenVR.Compositor?.GetCumulativeStats(ref Before, Size);
            var Frames = new System.Collections.Generic.List<float>();
            double Start = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < Start + 8) { yield return null; Frames.Add(Time.unscaledDeltaTime * 1000f); }
            Valve.VR.OpenVR.Compositor?.GetCumulativeStats(ref After, Size);
            Frames.Sort();
            float Avg = 0f; foreach (float F in Frames) Avg += F; Avg /= Mathf.Max(1, Frames.Count);
            float P95 = Frames.Count > 0 ? Frames[(int)(Frames.Count * .95f)] : 0f;
            Vars.Log.LogWarning($"Streamer test, view {Label}: {Frames.Count} frames in 8 s, avg {Avg:0.00} ms, 95% {P95:0.00} ms; SteamVR dropped {After.m_nNumDroppedFrames - Before.m_nNumDroppedFrames}, reprojected {After.m_nNumReprojectedFrames - Before.m_nNumReprojectedFrames}.");
        }

        // Writes a 24-bit BMP (no image module needed) next to BepInEx's log, and logs its brightness and colour count.
        private static void SaveForTest(Texture2D Tex, string Name)
        {
            Color32[] P = Tex.GetPixels32();
            int W = Tex.width, H = Tex.height, Row = (W * 3 + 3) & ~3;
            byte[] B = new byte[54 + Row * H];
            B[0] = (byte)'B'; B[1] = (byte)'M';
            BitConverter.GetBytes(B.Length).CopyTo(B, 2); BitConverter.GetBytes(54).CopyTo(B, 10); BitConverter.GetBytes(40).CopyTo(B, 14);
            BitConverter.GetBytes(W).CopyTo(B, 18); BitConverter.GetBytes(H).CopyTo(B, 22); B[26] = 1; B[28] = 24;
            double Sum = 0; var Colours = new System.Collections.Generic.HashSet<int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 C = P[y * W + x]; int I = 54 + y * Row + x * 3;
                    B[I] = C.b; B[I + 1] = C.g; B[I + 2] = C.r;
                    if (((x | y) & 7) == 0) { Sum += (C.r + C.g + C.b) / 3.0; Colours.Add(C.r << 16 | C.g << 8 | C.b); }
                }
            string Path_ = Path.Combine(BepInEx.Paths.BepInExRootPath, Name);
            File.WriteAllBytes(Path_, B);
            Vars.Log.LogWarning($"Streamer test: {Name} {W}x{H}, mean brightness {Sum / Math.Max(1, (W / 8) * (H / 8)):0}/255, {Colours.Count} colours in the samples.");
        }

        // A line "perftest": times every per-frame method of the mod (Update, LateUpdate, FixedUpdate of its
        // MonoBehaviours, and the camera postfix that places the eyes) with test-only Harmony timers, records frame
        // times and SteamVR's dropped and reprojected frames over 10 s, and logs the totals and the top costs.
        private static bool PerfTest;
        private static readonly System.Collections.Generic.Dictionary<string, double> PerfTicks = new System.Collections.Generic.Dictionary<string, double>();
        private static void PerfStart(out long __state) => __state = System.Diagnostics.Stopwatch.GetTimestamp();
        private static void PerfStop(long __state, System.Reflection.MethodBase __originalMethod)
        {
            string Key = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
            PerfTicks.TryGetValue(Key, out double V);
            PerfTicks[Key] = V + (System.Diagnostics.Stopwatch.GetTimestamp() - __state);
        }
        private static IEnumerator MeasurePerformance(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 5) yield return null;

            var H = new HarmonyLib.Harmony("vrtrakill.devtest.perf");
            var Pre = new HarmonyLib.HarmonyMethod(typeof(DevTestHooks), nameof(PerfStart));
            var Post = new HarmonyLib.HarmonyMethod(typeof(DevTestHooks), nameof(PerfStop));
            int Patched = 0;
            foreach (Type Ty in typeof(DevTestHooks).Assembly.GetTypes())
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(Ty) || Ty.IsAbstract || Ty.ContainsGenericParameters) continue;
                foreach (string Name in new[] { "Update", "LateUpdate", "FixedUpdate", "OnPreRender", "OnPostRender" })
                {
                    var M = Ty.GetMethod(Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                    if (M == null || M.GetMethodBody() == null) continue;
                    try { H.Patch(M, prefix: Pre, postfix: Post); Patched++; } catch { }
                }
            }
            foreach (var M in new[] { HarmonyLib.AccessTools.Method(typeof(VRCamera.Patches.CameraConverterP), "HandleRotationsAndPositions") })
                if (M != null) { H.Patch(M, prefix: Pre, postfix: Post); Patched++; }

            Valve.VR.Compositor_CumulativeStats Before = new Valve.VR.Compositor_CumulativeStats(), After = new Valve.VR.Compositor_CumulativeStats();
            uint Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Valve.VR.Compositor_CumulativeStats));
            Valve.VR.OpenVR.Compositor?.GetCumulativeStats(ref Before, Size);
            PerfTicks.Clear();
            var Frames = new System.Collections.Generic.List<float>();
            double Start = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < Start + 10) { yield return null; Frames.Add(Time.unscaledDeltaTime * 1000f); }
            Valve.VR.OpenVR.Compositor?.GetCumulativeStats(ref After, Size);
            H.UnpatchSelf();

            Frames.Sort();
            float Avg = 0f; foreach (float F in Frames) Avg += F; Avg /= Mathf.Max(1, Frames.Count);
            float P95 = Frames.Count > 0 ? Frames[(int)(Frames.Count * .95f)] : 0f, Max = Frames.Count > 0 ? Frames[Frames.Count - 1] : 0f;
            int Over = Frames.FindAll(F => F > 11.2f).Count;
            double TickMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double ModMs = 0; foreach (double V in PerfTicks.Values) ModMs += V * TickMs;
            var Top = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(PerfTicks);
            Top.Sort((A, B) => B.Value.CompareTo(A.Value));
            string TopText = string.Join(", ", Top.GetRange(0, Math.Min(6, Top.Count)).ConvertAll(K => $"{K.Key} {K.Value * TickMs / Frames.Count:0.000} ms"));
            Vars.Log.LogWarning($"Perf: scene {SceneHelper.CurrentScene}, frame cap {Application.targetFrameRate}, vsync {QualitySettings.vSyncCount}, {Frames.Count} frames in 10 s, frame avg {Avg:0.00} ms, 95% {P95:0.00} ms, max {Max:0.0} ms, {Over} over 11.2 ms; "
                              + $"SteamVR presents {After.m_nNumFramePresents - Before.m_nNumFramePresents}, dropped {After.m_nNumDroppedFrames - Before.m_nNumDroppedFrames}, reprojected {After.m_nNumReprojectedFrames - Before.m_nNumReprojectedFrames}; "
                              + $"mod per-frame code {ModMs / Math.Max(1, Frames.Count):0.000} ms/frame over {Patched} methods; top: {TopText}");
            Vars.Log.LogWarning("Perf test: done.");
        }

        // A line "levelsweep": loads every level of the game in turn (the scene in the first line only starts it) and,
        // for each, logs whether it loaded, the player and the VR eyes are there, the exceptions it raised, and the
        // frame time, plus "Sweep view: <scene>" for a capture. Catches level-specific breakage: portals, special
        // cameras, the platformer and fishing levels, Cyber Grind, the sandbox. Also shows a subtitle in the first one.
        private static bool LevelSweep;
        private static int Exceptions;
        private static readonly string[] SweepScenes =
        {
            "Tutorial", "Level 0-1", "Level 0-2", "Level 0-3", "Level 0-4", "Level 0-5", "Level 1-1", "Level 1-2", "Level 1-3", "Level 1-4",
            "Level 2-1", "Level 2-2", "Level 2-3", "Level 2-4", "Level 3-1", "Level 3-2", "Level 4-1", "Level 4-2", "Level 4-3", "Level 4-4",
            "Level 5-1", "Level 5-2", "Level 5-3", "Level 5-4", "Level 6-1", "Level 6-2", "Level 7-1", "Level 7-2", "Level 7-3", "Level 7-4",
            "Level 8-1", "Level 8-2", "Level 8-3", "Level 8-4", "Level 9-1", "Level 9-2", "Level P-1", "Level P-2", "Level P-3",
            "Endless", "uk_construct", "Level 0-S", "Level 1-S", "Level 2-S", "Level 3-S", "Level 4-S", "Level 5-S", "Level 6-S", "Level 7-S",
        };
        private static IEnumerator SweepLevels(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            if (SubtitleController.Instance != null)
            {
                SubtitleController.Instance.DisplaySubtitle("VR subtitle check: can you read this line?");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .5) yield return null;
                Vars.Log.LogWarning("Sweep view: subtitle");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            }
            int Problems = 0;
            if (Directory.Exists(SavesCopy)) Directory.Delete(SavesCopy, true);
            CopyTree(SavesPath, SavesCopy);
            foreach (string S in SweepScenes)
            {
                int Before = Exceptions;
                try { SceneHelper.LoadScene(S); }
                catch (Exception E) { Vars.Log.LogWarning($"Sweep: {S} could not start loading: {E.Message}"); Problems++; continue; }
                T = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < T + 45 && SceneHelper.CurrentScene != S) yield return null;
                bool Loaded = SceneHelper.CurrentScene == S;
                T = Clock.Elapsed.TotalSeconds;
                while (Loaded && Clock.Elapsed.TotalSeconds < T + 15 && Vars.Registered<NewMovement>() == null && MonoSingleton<PlatformerMovement>.Instance == null) yield return null;
                T = Clock.Elapsed.TotalSeconds;
                int Frames = 0; float Worst = 0f;
                while (Clock.Elapsed.TotalSeconds < T + 6) { yield return null; Frames++; Worst = Mathf.Max(Worst, Time.unscaledDeltaTime * 1000f); }
                Camera L = VRCamera.Patches.CameraConverterP.leftEye;
                bool Eyes = L != null && L.isActiveAndEnabled;
                bool Player = Vars.Registered<NewMovement>() != null || MonoSingleton<PlatformerMovement>.Instance != null;
                int New = Exceptions - Before;
                bool Ok = Loaded && Eyes && New == 0;
                if (!Ok) Problems++;
                Vars.Log.LogWarning($"Sweep: {S} -> {(Ok ? "OK" : "PROBLEM")} (loaded {Loaded}, player {Player}, VR eyes {Eyes}, exceptions {New}, {Frames / 6f:0} fps, worst frame {Worst:0} ms)");
                if (Loaded) Vars.Log.LogWarning($"Sweep view: {S}");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2.5) yield return null;
            }
            Vars.Log.LogWarning($"Sweep: {SweepScenes.Length} scenes, {Problems} with a problem; saves put back as they were ({RestoreSaves()} file(s) restored or removed). Sweep: done.");
        }

        // A line "pausedump": opens the pause menu and logs its object tree (names, active state, rect sizes, and the
        // UI components), to build UI that matches it.
        private static bool PauseDump;
        private static void DumpTree(Transform T, int Depth, System.Text.StringBuilder SB)
        {
            if (Depth > 6) return;
            RectTransform R = T as RectTransform;
            string Comps = string.Join(",", Array.ConvertAll(T.GetComponents<Component>(), C => C == null ? "?" : C.GetType().Name));
            string Text = T.GetComponent<TMPro.TMP_Text>() is TMPro.TMP_Text TT ? $" \"{TT.text}\" size {TT.fontSize}" : T.GetComponent<UnityEngine.UI.Text>() is UnityEngine.UI.Text UT ? $" \"{UT.text}\"" : "";
            SB.Append(new string(' ', Depth * 2)).Append(T.name).Append(T.gameObject.activeSelf ? "" : " (off)")
              .Append(R != null ? $" rect {R.rect.width:0}x{R.rect.height:0} pos {R.anchoredPosition.x:0},{R.anchoredPosition.y:0} anchors {R.anchorMin}-{R.anchorMax} pivot {R.pivot}" : "")
              .Append(" [").Append(Comps).Append("]").Append(Text).Append('\n');
            foreach (Transform C in T) DumpTree(C, Depth + 1, SB);
        }
        private static IEnumerator DumpPause(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            MonoSingleton<OptionsManager>.Instance.Pause();
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            GameObject PM = MonoSingleton<OptionsManager>.Instance.pauseMenu;
            var SB = new System.Text.StringBuilder();
            if (PM != null)
            {
                Transform Top = PM.transform.parent != null ? PM.transform.parent : PM.transform;
                SB.Append($"pause menu {PM.name}, parent {Top.name}, canvas {PM.GetComponentInParent<Canvas>()?.rootCanvas?.name}\n");
                DumpTree(Top, 0, SB);
            }
            Vars.Log.LogWarning("Pause dump:\n" + SB);
            MonoSingleton<OptionsManager>.Instance.UnPause();
            Vars.Log.LogWarning("Pause dump: done.");
        }

        // A line "vrsettingstest": pauses, logs "VR settings: open" (capture) with where the VR column is, aims the laser
        // at each of its buttons (a hover tick each), then presses each one and checks its label changed and the config
        // file was saved. It changes the config file: back it up and restore it afterwards.
        private static bool VRSettingsTest;
        private static IEnumerator CheckVRSettings(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            bool ArmLeft = Feedback.Haptics.ArmIsLeft;
            Pose Rest = new Pose(new Vector3(ArmLeft ? .08f : -.08f, -.08f, .3f), Quaternion.identity);
            Pose Down = new Pose(new Vector3(ArmLeft ? -.25f : .25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            if (ArmLeft) { VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = Rest; VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = Down; }
            else { VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = Rest; VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = Down; }
            MonoSingleton<OptionsManager>.Instance.Pause();
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            UI.VRSettingsMenu Menu = MonoSingleton<OptionsManager>.Instance.GetComponent<UI.VRSettingsMenu>();
            Transform Column = Menu != null ? MonoSingleton<OptionsManager>.Instance.pauseMenu.transform.parent.Find("VR Settings") : null;
            Camera L = VRCamera.Patches.CameraConverterP.leftEye;
            if (Column == null) { Vars.Log.LogWarning("VR settings test: no VR column. VR settings test: done."); yield break; }
            Vector3 Dir = Column.position - L.transform.position;
            UnityEngine.UI.Button[] Buttons = Column.GetComponentsInChildren<UnityEngine.UI.Button>();
            Vars.Log.LogWarning($"VR settings: open ({Buttons.Length} buttons, {Dir.magnitude:0.0} m away, {Vector3.Angle(L.transform.forward, Dir):0} deg off the gaze, shown {Column.GetComponent<CanvasGroup>().alpha})");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;

            // laser hover over each VR button
            Valve.VR.SteamVR_Input_Sources GunSource = ArmLeft ? Valve.VR.SteamVR_Input_Sources.RightHand : Valve.VR.SteamVR_Input_Sources.LeftHand;
            Controllers.VRControllersSystem Hand = Controllers.VRControllersSystem.Get(GunSource);
            Quaternion Rig = Hand.transform.rotation * Quaternion.Inverse(Rest.rotation);
            Quaternion Off = Quaternion.Inverse(Hand.transform.rotation) * Hand.GunOffset.transform.rotation;
            Feedback.Haptics.Recording = true; Feedback.Haptics.TestLog.Clear();
            foreach (UnityEngine.UI.Button B in Buttons)
            {
                RectTransform RT = (RectTransform)B.transform;
                Vector3 Target = RT.TransformPoint(RT.rect.center);
                Quaternion Want = Quaternion.LookRotation(Target - Hand.GunOffset.transform.position, Rig * Vector3.up) * Quaternion.Inverse(Off);
                Pose P = new Pose(Rest.position, Quaternion.Inverse(Rig) * Want);
                if (ArmLeft) VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = P; else VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = P;
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .35) yield return null;
            }
            int Ticks = Feedback.Haptics.TestLog.FindAll(S => S.StartsWith("menu hover")).Count;
            Vars.Log.LogWarning($"VR settings test, laser: {Ticks} hover tick(s) over {Buttons.Length} buttons -> {(Ticks >= Buttons.Length ? "OK" : "DIFFERENT")}");
            Feedback.Haptics.Recording = false;

            // press each button: its label must change (except the recenter action) and the file must be written
            string File_ = Prefs.ConfigMaster.ConfigPath;
            DateTime Written = File.GetLastWriteTimeUtc(File_);
            int Changed = 0;
            var Report = new System.Text.StringBuilder();
            foreach (UnityEngine.UI.Button B in Buttons)
            {
                TMPro.TMP_Text Label = B.GetComponentInChildren<TMPro.TMP_Text>();
                string Before = Label.text;
                B.onClick.Invoke();
                yield return null;
                // RECENTER acts at once and CHEATS opens the game's cheat menu (in a level): neither label changes
                if (Label.text != Before || Before.StartsWith("RECENTER") || Before.StartsWith("CHEATS")) Changed++;
                Report.Append($"[{Before} -> {Label.text}] ");
            }
            bool Saved = File.GetLastWriteTimeUtc(File_) > Written;
            // COMFORT was pressed first; the presses after it changed turning, the vignette and flashes by hand
            string Comfort = "none";
            foreach (UnityEngine.UI.Button B in Buttons)
            {
                string Text = B.GetComponentInChildren<TMPro.TMP_Text>().text;
                if (Text.StartsWith("COMFORT:")) Comfort = Text; // not "COMFORT VIGNETTE"
            }
            Vars.Log.LogWarning($"VR settings test, preset after manual changes: {Comfort} -> {(Comfort.EndsWith("CUSTOM") ? "OK" : "DIFFERENT")}");
            Vars.Log.LogWarning($"VR settings test, presses: {Changed} of {Buttons.Length} changed, config saved {Saved}: {Report} -> {(Changed == Buttons.Length && Saved ? "OK" : "DIFFERENT")}");
            Vars.Log.LogWarning("VR settings: pressed");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
            MonoSingleton<OptionsManager>.Instance.UnPause();
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            // the comfort vignette: the same still view with it off, then at full strength (amount forced), for an A/B
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = new Pose(new Vector3(-.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = new Pose(new Vector3(.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            Vars.Config.Comfort.Vignette = true; Vars.Config.Comfort.VignetteStrength = 1f;
            VRCamera.ComfortVignette.TestAmount = 0f;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1) yield return null;
            Vars.Log.LogWarning("VR settings: vigoff");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3.5) yield return null;
            VRCamera.ComfortVignette.TestAmount = 1f;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1) yield return null;
            Vars.Log.LogWarning("VR settings: vigon");
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3.5) yield return null;
            VRCamera.ComfortVignette.TestAmount = null;
            Vars.Config.Comfort.Vignette = false;
            // a subtitle, switched on in memory only (the player's preference is not changed), hands lowered
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = new Pose(new Vector3(-.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = new Pose(new Vector3(.25f, -.6f, .1f), Quaternion.Euler(60, 0, 0));
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 1.5) yield return null;
            if (SubtitleController.Instance != null)
            {
                bool Was = SubtitleController.Instance.SubtitlesEnabled;
                SubtitleController.Instance.SubtitlesEnabled = true;
                SubtitleController.Instance.DisplaySubtitle("Subtitle check: can you read this line in the headset?");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + .6) yield return null;
                Subtitle Line = UnityEngine.Object.FindObjectOfType<Subtitle>();
                Camera Eye = VRCamera.Patches.CameraConverterP.leftEye;
                if (Line != null && Eye != null)
                {
                    Vector3 D = Line.transform.position - Eye.transform.position;
                    Canvas SC = Line.GetComponentInParent<Canvas>();
                    Vars.Log.LogWarning($"VR settings: subtitle ({D.magnitude:0.0} m away, {Vector3.Angle(Eye.transform.forward, D):0} deg off the gaze, {Vector3.SignedAngle(Eye.transform.forward, D, Eye.transform.right):0} deg below, canvas {SC?.rootCanvas?.name} {SC?.rootCanvas?.renderMode})");
                }
                else Vars.Log.LogWarning("VR settings: subtitle (no subtitle object found)");
                T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 3) yield return null;
                SubtitleController.Instance.SubtitlesEnabled = Was;
            }
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = null;
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = null;
            Vars.Log.LogWarning("VR settings test: done.");
        }

        // A line "prompttest": for each controller type the mod has a layout for, asks the game for the binding text of
        // its main actions (the text its prompts show) and logs it, then the same for PS VR2 with "Left handed" on.
        private static bool PromptTest;
        private static string SafeBinding(InputManager IM, string A)
        {
            try { return IM.GetBindingString(A); }
            catch (Exception E) { return "(" + E.GetType().Name + ", action " + (IM.InputSource.Actions.FindAction(A) == null ? "not found" : "found") + ")"; }
        }
        private static IEnumerator CheckPrompts(System.Diagnostics.Stopwatch Clock)
        {
            double T = Clock.Elapsed.TotalSeconds;
            while (Clock.Elapsed.TotalSeconds < T + 60 && (Vars.IsMainMenu || Vars.Registered<NewMovement>() == null)) yield return null;
            T = Clock.Elapsed.TotalSeconds; while (Clock.Elapsed.TotalSeconds < T + 2) yield return null;
            InputManager IM = MonoSingleton<InputManager>.Instance;
            string[] Actions = { "Jump", "Dodge", "Slide", "Punch", "Hook", "Primary Fire", "Secondary Fire", "Change Fist", "Next Variation", "Last Used Weapon", "Pause", "Move", "Look", "Revolver" };
            Vars.Log.LogWarning("Prompt test, flat text: " + string.Join(", ", Array.ConvertAll(Actions, A => A + "=" + SafeBinding(IM, A))));
            string[] Types = { "playstation_vr2_sense", "oculus_touch", "knuckles", "frame_controller", "pico_controller", "vive_controller", "holographic_controller" };
            bool WasLeft = Vars.Config.Controllers.LeftHanded;
            foreach (bool Left in new[] { false, true })
            {
                Vars.Config.Controllers.LeftHanded = Left;
                foreach (string Ty in Types)
                {
                    if (Left && Ty != "playstation_vr2_sense" && Ty != "frame_controller") continue;
                    UI.Patches.ButtonPrompts.TestControllerType = Ty;
                    Vars.Log.LogWarning($"Prompt test, {Ty}{(Left ? " left handed" : "")}: " + string.Join(", ", Array.ConvertAll(Actions, A => A + "=" + SafeBinding(IM, A))));
                    yield return null;
                }
            }
            // by action id too (HudMessage and TextGetControl pass the Guid)
            UI.Patches.ButtonPrompts.TestControllerType = "playstation_vr2_sense";
            Vars.Config.Controllers.LeftHanded = false;
            Vars.Log.LogWarning($"Prompt test, by id: Jump={IM.GetBindingString(IM.InputSource.Jump.Action.id)}, Punch={IM.GetBindingString(IM.InputSource.Punch.Action.id)}");
            UI.Patches.ButtonPrompts.TestControllerType = null;
            Vars.Config.Controllers.LeftHanded = WasLeft;
            Vars.Log.LogWarning("Prompt test: done.");
        }

        // A second line "jumptest" in the file: once the player has stood on the ground for 2 s, jump three times through
        // the game's own NewMovement.Jump and log the height reached, with the walk speed and jump power in use.
        private static bool JumpTest;
        private static IEnumerator MeasureJumps(System.Diagnostics.Stopwatch Clock)
        {
            for (int Jump = 1; Jump <= 3; Jump++)
            {
                double GroundedSince = -1, Start = Clock.Elapsed.TotalSeconds;
                NewMovement NM = null;
                while (Clock.Elapsed.TotalSeconds < Start + 60)
                {
                    NM = Vars.Registered<NewMovement>();
                    bool Grounded = NM != null && NM.gc != null && NM.gc.touchingGround && NM.rb != null && NM.rb.velocity.sqrMagnitude < 0.01f;
                    if (!Grounded) GroundedSince = -1;
                    else if (GroundedSince < 0) GroundedSince = Clock.Elapsed.TotalSeconds;
                    else if (Clock.Elapsed.TotalSeconds - GroundedSince > 2) break;
                    yield return null;
                }
                if (NM == null || GroundedSince < 0) { Vars.Log.LogWarning("Jump test: the player never stood still on the ground."); yield break; }

                float Y0 = NM.rb.position.y, Peak = Y0;
                NM.Jump();
                double JumpStart = Clock.Elapsed.TotalSeconds;
                while (Clock.Elapsed.TotalSeconds < JumpStart + 2.5)
                {
                    if (NM == null || NM.rb == null) yield break;
                    Peak = Mathf.Max(Peak, NM.rb.position.y);
                    yield return null;
                }
                Vars.Log.LogWarning($"Jump test {Jump}: height {Peak - Y0:0.00} m (jumpPower {NM.jumpPower:0.##}, walkSpeed {NM.walkSpeed:0.##}, "
                                  + $"\"Movement multiplier\" {Vars.Config.MovementMultiplier}, \"Jump multiplier\" {Vars.Config.JumpMultiplier}).");
            }
        }
    }
}
