using UnityEngine;

namespace Plugin.Systems
{
    // A hidden object that lives for the whole session and runs the mod's coroutines and overlay.
    // The BepInEx manager object (the plugin's own GameObject) is destroyed shortly after the game starts
    // (seen on build 22957324: right after "Chainloader startup complete"). Coroutines started on it died with it:
    // the SteamVR retry loop stopped after the first try, so a game started before SteamVR was ready stayed flat,
    // and the late canvas conversion in SceneWorker never ran. Hidden like BepInEx's HideManagerGameObject option,
    // so scene cleanup code does not find it.
    internal sealed class ModHost : MonoBehaviour
    {
        public static ModHost Instance { get; private set; }
        private bool quitting;

        public static ModHost Create()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("VRTRAKILL Host") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ModHost>();
            return Instance;
        }

        // The game filters Unity exceptions out of its log (the plugin sets filterLogType to Warning), so an exception in
        // the VR start loop would stop it silently; log the first few to BepInEx's log instead.
        private int loggedErrors;
        private void Update()
        {
            try { Plugin.TickVRStart(); }
            catch (System.Exception e) { if (loggedErrors++ < 3) Vars.Log.LogError("VR start step failed: " + e); }
            try { Input.SVRActionsManager.TickVirtualInput(); VRCamera.FramePacing.Apply(); }
            catch (System.Exception e) { if (loggedErrors++ < 3) Vars.Log.LogError("Virtual input step failed: " + e); }
        }

        // Sends controller vibration once a frame, after the game's rumble and the mod's pulses for this frame are in.
        private void LateUpdate()
        {
            if (Vars.Config == null) return;
            try { Feedback.LowHealth.Tick(); }
            catch (System.Exception e) { if (loggedErrors++ < 3) Vars.Log.LogError("Low health warning step failed: " + e); }
            try { Feedback.AdaptiveTriggers.Tick(); }
            catch (System.Exception e) { if (loggedErrors++ < 3) Vars.Log.LogError("Adaptive trigger step failed: " + e); }
            try { Feedback.Haptics.Flush(); }
            catch (System.Exception e) { if (loggedErrors++ < 3) Vars.Log.LogError("Haptics step failed: " + e); }
        }

        private void OnApplicationQuit()
        {
            quitting = true;
            Input.SVRActionsManager.ReleaseAll(); // a key held at exit would stay down in Windows
            try { Feedback.AdaptiveTriggers.Shutdown(); } catch { } // free the triggers for the next game
        }

        // With "Windows key presses", held keys must not stay down for the program that gets focus.
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Input.SVRActionsManager.OnFocusLost();
        }

        private void OnDestroy()
        {
            if (!quitting) Vars.Log.LogError("The VRTRAKILL host object was destroyed. VR start retries and late UI conversion stop.");
        }
    }
}
