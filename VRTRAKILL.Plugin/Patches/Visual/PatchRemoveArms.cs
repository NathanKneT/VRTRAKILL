using HarmonyLib;
using Plugin.Systems;
using Plugin.Systems.Arms;
using Sandbox.Arm;
using System;
using System.Linq;
using UnityEngine;

namespace Plugin.Patches.Visual;

// Hides the game's arm on the shotgun and adds the VR arm transformer to the revolver, sandbox arm and fishing rod.
[HarmonyPatch]
internal class PatchRemoveArms
{
    // Adds the VR arm transformer to the revolver arm.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Revolver), nameof(Revolver.Start))]
    static void RemoveRevolverArm(Revolver __instance)
    {
        try { __instance.gameObject.AddComponent<VRArmTransformer>(); }
        catch (NullReferenceException) { Vars.Log.LogWarning($"Revolver is null???"); }
    }
    // Adds the VR arm transformer to the sandbox arm.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SandboxArm), nameof(SandboxArm.Awake))]
    static void RemoveSandboxArm(SandboxArm __instance)
    {
        try { __instance.gameObject.AddComponent<VRArmTransformer>(); }
        catch (NullReferenceException) { Vars.Log.LogWarning($"Sandbox arm is null???"); }
    }
    // Hides the game's right arm on the shotgun. It sat under "Shotgun_New" in 1.x; the current model is
    // "ShogunNewAnims" (see PatchGunHands), so the arm is looked for anywhere in the shotgun. A part that holds the gun
    // bones is never hidden. If nothing matches, the shotgun's parts are logged once, so the name can be found.
    private static bool LoggedShotgunParts;
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.Start))]
    static void RemoveShotgunArm(Shotgun __instance)
    {
        Transform Arm = __instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(T => T.name == "RightArm");
        Transform GunBone = __instance.transform.childCount > 2 ? __instance.transform.GetChild(2) : null;
        if (Arm != null && !(GunBone != null && GunBone.IsChildOf(Arm))) { Arm.gameObject.SetActive(false); return; }
        if (LoggedShotgunParts) return;
        LoggedShotgunParts = true;
        Vars.Log.LogWarning("No right arm found to hide on the shotgun; its parts: "
            + string.Join(", ", __instance.GetComponentsInChildren<Transform>(true).Where(T => Depth(T, __instance.transform) <= 3).Select(T => T.name)));
    }
    private static int Depth(Transform T, Transform Root)
    {
        int D = 0;
        for (; T != null && T != Root; T = T.parent) D++;
        return D;
    }
    // Adds the VR arm transformer to the fishing rod arm.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FishingRodWeapon), nameof(FishingRodWeapon.Awake))]
    static void RemoveFRArm(FishingRodWeapon __instance)
    {
        try { __instance.gameObject.AddComponent<VRArmTransformer>(); }
        catch (NullReferenceException) { Vars.Log.LogWarning("Fishing rod is null???"); }
    }
}
