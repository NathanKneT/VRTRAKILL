using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using Valve.VR;
using Plugin.Systems;
using VRTRAKILL.Utilities;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Unity.XR.OpenVR;
using System.Collections.Generic;
using System;
using System.Collections;

namespace Plugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }
        private static XRGeneralSettings generalSettings;
        private static XRManagerSettings managerSettings;
        private static OpenVRLoader xrLoader;
        private const float VRInitRetryDelay = 2f;
        // PreInitialize creates new action objects; a second call orphans the ones already handed out
        private static bool ActionsPreInitialized;

        // Loads the config, applies the patches, starts the scene worker, then starts VR init.
        public void Awake()
        {
            Log = Logger;
            Debug.unityLogger.filterLogType = LogType.Warning;
            // a VR game must keep running when its desktop window loses focus (SteamVR windows, dashboard, alt-tab):
            // with Unity's default the game froze, the headset image froze, and the SteamVR retry loop stopped
            Application.runInBackground = true;

            // a setup failure must not stop VR init, or the game falls back to SteamVR theater mode;
            // each step has its own try so a bad config or Binds.json does not also skip the patches
            Patches.Controllers.PatchControllerHaptics.WatchQuit();
            // coroutines and the overlay run on our own hidden object: the game destroys this plugin's GameObject
            // right after startup, which used to kill the SteamVR retry loop (see ModHost)
            ModHost host = ModHost.Create();
            try { StartupStatus.Install(host.gameObject); }
            catch (Exception e) { Log.LogError("VRTRAKILL startup status overlay failed: " + e); }
            try { Prefs.ConfigMaster.Init(); }
            catch (Exception e) { Log.LogError("VRTRAKILL config setup failed: " + e); }
            try { PatchStuff(); }
            catch (Exception e) { Log.LogError("VRTRAKILL patching failed: " + e); }
            try { SceneWorker.Init(host); }
            catch (Exception e) { Log.LogError("VRTRAKILL scene setup failed: " + e); }

            Systems.Updates.UpdateService.Begin(host); // background update check; never throws

            // first try now, before the first scene, like before: when SteamVR is up with a headset the probe answers at
            // once and VR starts here; otherwise ModHost.Update keeps trying in the background
            StartProbe();
            if (ProbeThread.Join(3000)) TickVRStart();
        }

        // ---- VR start ----
        // The old start called the blocking OpenVR init on the main thread every 2 s. With SteamVR not ready (headset off,
        // SteamVR in an error state) each call froze the game, seen for 40 s and more on the boot screen ("black screen,
        // won't load"), and the coroutine that ran the retries never resumed after its first wait at chainloader time.
        // Now a background thread asks SteamVR whether it runs and sees a headset (a "background" OpenVR app, which neither
        // starts SteamVR nor waits for it), and the real init runs on the main thread only once that answer is yes.
        // ModHost.Update drives it, every 2 s, until VR runs; there is no give-up, VR starts whenever the headset is ready.
        // Time.realtimeSinceStartup is not usable at chainloader time (the first try runs in Awake, before the first
        // frame): a retry scheduled from it never came, which is also why the old coroutine never resumed after its first
        // wait. A Stopwatch is a plain monotonic clock that works from the first instant.
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static float Now => (float)Clock.Elapsed.TotalSeconds;
        private static int VRAttempt;
        private static float NextVRAttemptAt;
        private static bool VRStarted;
        private static System.Threading.Thread ProbeThread;
        private static volatile bool ProbeDone;
        private static EVRInitError ProbeResult;
        private static string ProbeException; // the probe could not run (missing openvr_api, ...): fall back to the plain init
        // When SteamVR has just started, it may still be launching its own app (SteamVR Home, Room Setup). A game that
        // connects before that app finishes gets a Quit event from SteamVR and loses VR (seen in vrserver.txt). So a late
        // start waits until SteamVR has answered for a few seconds in a row.
        private const float SettleSeconds = 5f;
        private static float ProbeOkSince = -1f;
        private static XRDisplaySubsystem ActiveDisplay;
        private static bool ReportedVRStopped;
        private static bool InputInitialized;

        // Runs one step of the VR start: collects a finished probe, or starts the next one when the retry delay has passed.
        internal static void TickVRStart()
        {
            if (VRStarted) { WatchVR(); return; }
            if (ProbeThread != null)
            {
                if (!ProbeDone)
                {
                    // SteamVR in an error state ("Unexpected SteamVR Error", headset unplugged) can keep the check waiting
                    // for a long time; it runs on its own thread, so the game goes on, but say what is happening
                    if (!ReportedSlowProbe && Now - ProbeStartedAt > 5f)
                    {
                        ReportedSlowProbe = true;
                        LastInitError = "SteamVR is not answering. If it shows an error, check that the headset is connected and on (PS VR2: PlayStation VR2 App), then restart SteamVR.";
                        StartupStatus.ReportAttempt(Math.Max(VRAttempt, 2), LastInitError);
                        Log.LogWarning(LastInitError);
                    }
                    return;
                }
                ProbeThread = null;
                HandleProbeResult();
                return;
            }
            if (Now < NextVRAttemptAt) return;
            if (ProbeException != null) { VRAttempt++; TryFullInit(); return; }
            StartProbe();
        }

        // Starts the background SteamVR check.
        private static float ProbeStartedAt;
        private static bool ReportedSlowProbe;
        private static void StartProbe()
        {
            VRAttempt++;
            ProbeDone = false;
            ProbeStartedAt = Now;
            ReportedSlowProbe = false;
            ProbeThread = new System.Threading.Thread(() =>
            {
                try
                {
                    EVRInitError Error = EVRInitError.None;
                    OpenVR.InitInternal(ref Error, EVRApplicationType.VRApplication_Background);
                    if (Error == EVRInitError.None) OpenVR.ShutdownInternal();
                    ProbeResult = Error;
                }
                catch (Exception e) { ProbeException = e.GetType().Name + ": " + e.Message; }
                finally { ProbeDone = true; }
            })
            { IsBackground = true, Name = "VRTRAKILL SteamVR check" };
            ProbeThread.Start();
        }

        // Starts VR when SteamVR is ready, or records why not and schedules the next try.
        private static void HandleProbeResult()
        {
            if (ProbeException != null)
            {
                Log.LogWarning("The SteamVR check could not run (" + ProbeException + "); trying the full VR start instead.");
                TryFullInit();
                return;
            }
            if (ProbeResult != EVRInitError.None) ProbeOkSince = -1f;
            switch (ProbeResult)
            {
                case EVRInitError.None:
                    // at startup, start at once as before; later, let a freshly started SteamVR settle first
                    if (VRAttempt > 1)
                    {
                        float now = Now;
                        if (ProbeOkSince < 0f) ProbeOkSince = now;
                        if (now - ProbeOkSince < SettleSeconds)
                        {
                            LastInitError = "SteamVR sees the headset. Starting VR in a few seconds...";
                            RetryLater(1f);
                            return;
                        }
                    }
                    TryFullInit();
                    return;
                case EVRInitError.Init_NoServerForBackgroundApp:
                    if (VRAttempt == 1)
                    {
                        // the game's own (scene) init starts SteamVR with the game as its startup app, so SteamVR does not
                        // also launch Home or Room Setup, which would take VR away from the game; it blocks while SteamVR
                        // starts, like any VR game, and only on this first try
                        LastInitError = "SteamVR is not running.";
                        TryFullInit();
                        return;
                    }
                    LastInitError = "SteamVR is not running. Start SteamVR (PS VR2: start the PlayStation VR2 App).";
                    break;
                case EVRInitError.Init_HmdNotFound:
                case EVRInitError.Init_HmdNotFoundPresenceFailed:
                    LastInitError = "SteamVR does not see a headset. Turn the headset on (PS VR2: start the PlayStation VR2 App).";
                    break;
                default:
                    LastInitError = "SteamVR is not ready (" + ProbeResult + ").";
                    break;
            }
            RetryLater();
        }

        // Runs the real VR init on the main thread.
        private static void TryFullInit()
        {
            if (InitVRLoader(VRAttempt == 1 || VRAttempt % 30 == 0))
            {
                VRStarted = true;
                StartupStatus.ReportRunning();
                if (VRAttempt > 1) Log.LogMessage($"VR started on try {VRAttempt}.");
                string warning = StartupStatus.HeadsetWarning();
                if (warning != null) ModHost.Instance.StartCoroutine(Systems.Updates.UpdateNotice.ShowOnMainMenu(warning, 130));
                return;
            }
            RetryLater();
        }

        private static void RetryLater(float delay = VRInitRetryDelay)
        {
            StartupStatus.ReportAttempt(VRAttempt, LastInitError);
            if (VRAttempt == 1 || VRAttempt % 30 == 0) Log.LogWarning($"VR is not running yet (try {VRAttempt}): {LastInitError} Trying again every {VRInitRetryDelay:0} s.");
            NextVRAttemptAt = Now + delay;
        }

        // After VR started: if SteamVR stops it (another VR app took over, SteamVR closed), say so instead of
        // leaving a frozen headset image. Restarting XR in the same process is not safe with the SteamVR input
        // plugin, so the message asks for a game restart.
        private static float NextWatchAt;
        private static void WatchVR()
        {
            if (ReportedVRStopped || ActiveDisplay == null || Now < NextWatchAt) return;
            NextWatchAt = Now + 1f;
            if (ActiveDisplay.running) return;
            ReportedVRStopped = true;
            Log.LogError("SteamVR stopped VR for ULTRAKILL (another VR app started, or SteamVR closed). Restart ULTRAKILL to get VR back.");
            StartupStatus.ReportStopped("SteamVR stopped VR for ULTRAKILL (another VR app started, or SteamVR closed). Restart ULTRAKILL to get VR back.");
        }

        // The last VR init failure, in short, for the desktop status overlay.
        private static string LastInitError = "";

        // Applies the Harmony patches in the mod patch namespaces, and the avatar patches when the avatar is enabled.
        private void PatchStuff()
        {
            System.Collections.Generic.List<string> Namespaces = new System.Collections.Generic.List<string>
            {
                typeof(Patches.Controllers.A).Namespace,
                typeof(Patches.Misc.A).Namespace,
                typeof(Patches.ULTRAKILL.A).Namespace,
                typeof(Patches.Visual.A).Namespace,

                typeof(Systems.VRCamera.Patches.A).Namespace,
                typeof(Systems.UI.Patches.A).Namespace,
                typeof(Systems.Movement.Patches.A).Namespace,
            };
            System.Collections.Generic.List<System.Type> Types = new System.Collections.Generic.List<System.Type>
            {
            };
            // the avatar is opt-in; with it off nothing in its namespace is patched
            if (Vars.Config.EnableVRBody) Namespaces.Add(typeof(Systems.VRAvatar.Patches.A).Namespace);

            Patcher patcher = new Patcher(new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID))
            {
                Namespaces = Namespaces.ToArray(),
                Types = Types.ToArray(),
                Log = Vars.Log,
            };
            patcher.PatchAll();
            StartupStatus.PatchProblems += patcher.Failures;
            if (patcher.Failures > 0) Log.LogError($"{patcher.Failures} patch type(s) failed to apply; the game may have been updated.");
        }

        // Starts OpenVR through XR Management and SteamVR, then SteamVR input; returns false on failure.
        public static bool InitVRLoader(bool logFailure = true)
        {
            try
            {
                Log.LogMessage("INITILIZING");
                if (!ActionsPreInitialized) { SteamVR_Actions.PreInitialize(); ActionsPreInitialized = true; }

                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                managerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                xrLoader = ScriptableObject.CreateInstance<OpenVRLoader>();

                var settings = OpenVRSettings.GetSettings();
                if (settings == null) throw new InvalidOperationException("OpenVR settings not found.");
                settings.StereoRenderingMode = OpenVRSettings.StereoRenderingModes.MultiPass;

                generalSettings.Manager = managerSettings;

                // the game has no serialized XR loader asset, so fill the list directly (TrySetLoaders rejects runtime loaders)
                var activeLoaders = managerSettings.activeLoaders as List<XRLoader>;
                if (activeLoaders == null) throw new InvalidOperationException("XR loader list is not writable.");
                activeLoaders.Clear();
                activeLoaders.Add(xrLoader);
                managerSettings.InitializeLoaderSync();

                Log.LogMessage("Active loader: " + managerSettings.activeLoader);

                XRLoader loader = managerSettings.activeLoader;
                if (loader == null)
                {
                    // activeLoader can stay unset for a manager created at runtime; initialize the loader directly
                    if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
                    {
                        try { SteamVR.Initialize(true); }
                        catch (Exception e) { Log.LogDebug("SteamVR init deferred: " + e.Message); }
                    }
                    if (!xrLoader.Initialize())
                        throw new InvalidOperationException("OpenVR loader failed to initialize (SteamVR: " + SteamVR.initializedState + ").");
                    loader = xrLoader;
                }

                if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
                    SteamVR.Initialize(true);
                Log.LogMessage("SteamVR initialized. Active: " + SteamVR.active + " Connected: " + SteamVR.initializedState);

                Log.LogMessage("Initilizing:" + loader.Start());

                var display = loader.GetLoadedSubsystem<XRDisplaySubsystem>();
                if (display == null) throw new InvalidOperationException("OpenVR display subsystem is missing.");
                if (!display.running) display.Start();
                ActiveDisplay = display;

                // a failed earlier try leaves the SteamVR plugin disabled (_enabled = false, initializedState =
                // InitializeFailure), so SteamVR.instance stayed null even after VR started on a later try (seen:
                // "Active: False Connected: InitializeFailure"). OpenVR is up now; turning it back on creates the instance.
                if (!SteamVR.active)
                {
                    SteamVR.initializedState = SteamVR.InitializedStates.None;
                    SteamVR.enabled = true;
                    Log.LogMessage("SteamVR plugin restarted after an earlier failed try. Active: " + SteamVR.active + " Connected: " + SteamVR.initializedState);
                }

                // once per game: a second Init would add every SteamVR action listener twice (double key presses)
                if (!InputInitialized)
                {
                    SteamVR_Input.Initialize();
                    Systems.Input.SVRActionsManager.Init();
                    InputInitialized = true;
                }
                Log.LogMessage("DONE");
                return true;
            }
            catch (Exception e)
            {
                CleanupVRLoader();
                LastInitError = e.Message;
                if (logFailure) Log.LogWarning("OpenVR is not available yet: " + e.GetType().Name + " - " + e.Message);
                return false;
            }
        }

        // Deinitializes and destroys the XR loader objects so the next attempt starts clean.
        private static void CleanupVRLoader()
        {
            try
            {
                if (managerSettings != null && managerSettings.activeLoader != null)
                    managerSettings.DeinitializeLoader();
            }
            catch (Exception e) { Log?.LogDebug("OpenVR cleanup deferred: " + e.Message); }

            if (xrLoader != null) Destroy(xrLoader);
            if (managerSettings != null) Destroy(managerSettings);
            if (generalSettings != null) Destroy(generalSettings);

            xrLoader = null;
            managerSettings = null;
            generalSettings = null;
        }
    }
}
