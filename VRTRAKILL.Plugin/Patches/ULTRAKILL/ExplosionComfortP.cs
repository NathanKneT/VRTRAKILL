using HarmonyLib;
using UnityEngine;

namespace Plugin.Patches.ULTRAKILL
{
    // Softens explosions with "Screen flashes" on Reduced or Off. An explosion is a growing sphere drawn pure white for
    // its first 0.1 s, then in its colour, with a strong point light; a close one wraps the player, so in VR the white
    // frame is a flash over the whole view, and fights chain them ("lots of explosions, it's rough", PS VR2 test). The
    // sphere now starts in its colour, a little more see-through, and its light is half as strong. Size, damage and
    // timing are the game's.
    [HarmonyPatch]
    internal static class ExplosionComfortP
    {
        private const float SphereAlpha = .6f, LightIntensity = .5f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Explosion), nameof(Explosion.Start))]
        static void Soften(Explosion __instance)
        {
            if (Systems.UI.FlashLimiter.Cap >= 1f) return; // "Full": the game's look
            if (__instance.mr != null && __instance.originalMaterial != null)
            {
                __instance.whiteExplosion = false;
                __instance.mr.material = new Material(__instance.originalMaterial);
                __instance.materialColor.a *= SphereAlpha;
                __instance.mr.material.SetColor("_Color", __instance.materialColor);
            }
            if (__instance.light != null) __instance.light.intensity *= LightIntensity;
        }
    }
}
