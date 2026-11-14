using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.UI.Patches
{
    // The game's own screens (the shop, terminals) pick their button with ControllerPointer: a ray from the centre of
    // their canvas's event camera, clicked with Fire1. Canvases converted at level start get the hand's UI event
    // camera, but the shop canvas is switched on only when the player walks up to it, and kept the game's camera,
    // which in VR follows the head: the button the player looked at was selected while the laser pointed elsewhere,
    // and the laser was drawn to that gaze point (PS VR2 test, shop). The active screen now always uses the hand's
    // event camera, so the laser, the highlight and the click agree.
    [HarmonyPatch]
    internal static class GamePointerP
    {
        private static GraphicRaycaster LastRaycaster;
        private static Canvas LastCanvas;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ControllerPointer), "UpdateRaycasters")]
        static void UseHandCamera()
        {
            GraphicRaycaster Raycaster = ControllerPointer.raycaster;
            Camera Hand = UIConverter.UIEventCamera;
            if (Raycaster == null || Hand == null) return;
            if (Raycaster != LastRaycaster) { LastRaycaster = Raycaster; LastCanvas = Raycaster.GetComponent<Canvas>(); }
            if (LastCanvas != null && LastCanvas.renderMode != RenderMode.ScreenSpaceOverlay && LastCanvas.worldCamera != Hand)
                LastCanvas.worldCamera = Hand;
        }
    }
}
