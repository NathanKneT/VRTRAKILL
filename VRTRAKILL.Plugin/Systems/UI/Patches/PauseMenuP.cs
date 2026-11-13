using HarmonyLib;

namespace Plugin.Systems.UI.Patches
{
    [HarmonyPatch]
    internal static class PauseMenuP
    {
        // Adds the VR settings column beside the pause menu the first time the game pauses in a scene.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OptionsManager), nameof(OptionsManager.Pause))]
        static void AddVRSettings(OptionsManager __instance) => VRSettingsMenu.Attach(__instance);
    }
}
