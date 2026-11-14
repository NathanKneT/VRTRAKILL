using HarmonyLib;

namespace Plugin.Systems.UI.Patches
{
    // The VR column's CHEATS item opens the game's cheat consent screen from the pause menu (VRSettingsMenu). Accepting
    // or cancelling it hides it but leaves the game paused with nothing on screen; the pause menu comes back, so the
    // column, now saying CHEATS MENU, is there again.
    [HarmonyPatch]
    internal static class CheatsMenuP
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CheatsController), nameof(CheatsController.ActivateCheats))]
        [HarmonyPatch(typeof(CheatsController), nameof(CheatsController.Cancel))]
        static void BackToPause()
        {
            if (!UnityEngine.XR.XRSettings.isDeviceActive) return;
            OptionsManager O = Vars.Registered<OptionsManager>();
            if (O != null && O.paused && O.pauseMenu != null && !O.pauseMenu.activeSelf) O.pauseMenu.SetActive(true);
        }
    }
}
