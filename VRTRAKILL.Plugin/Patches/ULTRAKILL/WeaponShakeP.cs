using HarmonyLib;
using Plugin.Systems;
using UnityEngine;

namespace Plugin.Patches.ULTRAKILL
{
    // Calms the shake of a charging weapon. While a charge builds (the Piercer revolver, the shotgun's pump or grenade,
    // the railcannon, the rocket launcher's cannonball, the jackhammer's swing and saw), the game moves the weapon model
    // to a new random spot every frame, up to about 2 cm away. On a flat screen that reads as a tremble; in VR the
    // weapon is a scaled-down model in your hand, 40 cm from your eyes, so the same distance is a violent rattle ("some
    // weapons shake too much when I fire, it's unpleasant", PS VR2 test). The shake now keeps "Weapon charge shake"
    // of its size (a tenth by default), so a charge still shows without the rattle. Charge sounds, lights and timing
    // are the game's.
    [HarmonyPatch]
    internal static class WeaponShakeP
    {
        // the largest offset the game's shake makes is about 3.5 cm (the jackhammer swing); anything further is not
        // the shake, and is left alone
        private const float MaxShake = .1f;

        private static void Calm(Transform T, Vector3 Rest)
        {
            float Keep = Mathf.Clamp01(Vars.Config.Comfort.WeaponShake);
            if (Keep >= 1f || !UnityEngine.XR.XRSettings.isDeviceActive) return;
            Vector3 Offset = T.localPosition - Rest;
            if (Offset == Vector3.zero || Offset.sqrMagnitude > MaxShake * MaxShake) return;
            T.localPosition = Rest + Offset * Keep;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Revolver), nameof(Revolver.Update))]
        static void CalmRevolver(Revolver __instance)
        {
            if (__instance.wpos != null) Calm(__instance.transform, __instance.wpos.currentDefault);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.Update))]
        static void CalmShotgun(Shotgun __instance)
        {
            if (__instance.wpos != null) Calm(__instance.transform, __instance.wpos.currentDefault);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Railcannon), nameof(Railcannon.Update))]
        static void CalmRailcannon(Railcannon __instance)
        {
            if (__instance.wpos != null) Calm(__instance.transform, __instance.wpos.currentDefault);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RocketLauncher), nameof(RocketLauncher.Update))]
        static void CalmRocketLauncher(RocketLauncher __instance)
        {
            if (__instance.wpos != null) Calm(__instance.transform, __instance.wpos.currentDefault);
        }

        // the saw charge shakes the whole jackhammer; the swing charge shakes its model inside it
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShotgunHammer), nameof(ShotgunHammer.Update))]
        static void CalmJackhammer(ShotgunHammer __instance)
        {
            if (__instance.wpos != null) Calm(__instance.transform, __instance.wpos.currentDefault);
            if (__instance.modelTransform != null) Calm(__instance.modelTransform, __instance.defaultModelPosition);
        }
    }
}
