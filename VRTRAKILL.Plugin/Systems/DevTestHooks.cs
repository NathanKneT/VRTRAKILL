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
                }
            }
            catch (Exception e) { Vars.Log.LogWarning("Test scene file unreadable: " + e.Message); }
            if (string.IsNullOrEmpty(Scene)) return;
            Done = true;
            // the plugin filters Unity's exceptions out (filterLogType Warning); a test must see them
            Debug.unityLogger.filterLogType = LogType.Exception;
            Application.logMessageReceived += (Message, Stack, Type) =>
            { if (Type == LogType.Exception) Vars.Log.LogError("Test run exception: " + Message + " | " + Stack); };
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
            if (JumpTest) yield return MeasureJumps(Clock);
            if (PunchTest) yield return MeasurePunches(Clock);
            if (ShortcutTest) yield return PressSlideDashPause(Clock);
            if (AimTest) yield return MeasureAim(Clock);
            if (WhiplashTest) yield return MeasureWhiplash(Clock);
            if (WeaponView) yield return ShowWeapons(Clock);
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
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestRightPose = new Pose(new Vector3(.22f, -.38f, .38f), Quaternion.Euler(-45, 0, 0));
            VRBasePlugin.Systems.VRCamera.VRControllerLocations.TestLeftPose = new Pose(new Vector3(-.22f, -.38f, .38f), Quaternion.Euler(-45, 0, 0));
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
