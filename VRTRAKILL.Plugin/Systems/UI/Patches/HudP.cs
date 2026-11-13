using HarmonyLib;
using UnityEngine;

namespace Plugin.Systems.UI.Patches
{
    [HarmonyPatch]
    internal sealed class HudP
    {
        // Moves the standard HUD under the main canvas, unless it is the alt HUD.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HudController), nameof(HudController.Start))]
        static void ReparentHUD(HudController __instance)
        {
            if (__instance.altHud) return;
            else
            {
                __instance.transform.parent = CanvasController.Instance.transform;
                __instance.transform.localPosition = Vector3.zero;
                __instance.transform.localEulerAngles = Vector3.zero;
            }
        }
        // Converts a HUD canvas to world space, hides it in menus and sets VR positions for the gun and style panels.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDPos), nameof(HUDPos.CheckPos))]
        static void RetransformElements(HUDPos __instance)
        {
            if (__instance.GetComponent<Canvas>() == null) return;
            UIConverter.ConvertCanvas(__instance.GetComponent<Canvas>(), Force: true, DontAddComponent: true);
            __instance.gameObject.AddComponent<HideWhenMenuActive>();

            __instance.transform.localScale = new Vector3(.35f, .35f, .35f) * 0.1f;
            switch (__instance.gameObject.name)
            {
                case "GunCanvas":
                    WristHud.Attach(__instance); // on the wrist unless "HUD on wrist" is off; the positions below are the fallback
                    __instance.defaultPos = new Vector3(-440, -320, 120) * 0.1f;
                    __instance.defaultRot = new Vector3(0, -20, 0);

                    __instance.reversePos = new Vector3(440, -320, 120) * 0.1f;
                    __instance.reverseRot = new Vector3(0, 20, 0);
                    break;

                case "StyleCanvas":
                    __instance.defaultPos = new Vector3(260, 40, -240) * 0.1f;
                    __instance.defaultRot = new Vector3(356, 45, 357);

                    __instance.reversePos = new Vector3(-260, 40, -240) * 0.1f;
                    __instance.reverseRot = new Vector3(0, -45, 0);
                    break;
            }
        }
        // Converts the final rank canvas to world space and scales it for VR.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FinalRank), nameof(FinalRank.Start))]
        static void CFinalRank(FinalRank __instance)
        {
            // the canvas is normally the direct parent; search up the tree so a different layout does not throw here
            // (an exception left the rank screen unconverted, so it never showed in the headset: end-of-level issues)
            Canvas RankCanvas = __instance.transform.parent != null ? __instance.transform.parent.GetComponentInParent<Canvas>() : null;
            if (RankCanvas == null) { Vars.Log.LogWarning("Final rank screen: no canvas found above it; it is left as the game made it."); return; }
            UIConverter.ConvertCanvas(RankCanvas, Force: true);
            __instance.transform.localPosition = Vector3.zero;
            __instance.transform.localScale = new Vector3(8, 4, 4) * 0.3f;
        }
        // Makes the blood splatters on the screen bigger.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ScreenBlood), nameof(ScreenBlood.Start))]
        static void CScreenBlood(ScreenBlood __instance)
        {
            // makes blood splatters bigger for aesthetic reasons
            __instance.transform.localScale *= 10;
        }
        // Keeps the shop preview cameras mono; they draw the weapon and enemy models into a render texture, not the headset.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SmileRenderer), nameof(SmileRenderer.Setup))]
        static void MonoPreviewCamera(SmileRenderer __instance)
        {
            if (__instance.cam != null) __instance.cam.stereoTargetEye = StereoTargetEyeMask.None;
        }
    }
}
