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

        private void Update() => Plugin.TickVRStart();

        private void OnApplicationQuit()
        {
            quitting = true;
            Input.SVRActionsManager.ReleaseAll(); // a key held at exit would stay down in Windows
        }

        // The VR buttons are Windows key presses; held ones must not stay down for the program that gets focus.
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Input.SVRActionsManager.ReleaseAll();
        }

        private void OnDestroy()
        {
            if (!quitting) Vars.Log.LogError("The VRTRAKILL host object was destroyed. VR start retries and late UI conversion stop.");
        }
    }
}
