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
        // the first three levels named no arm on build 22957324, so the meshes and any part named like an arm or a hand
        // are listed at every depth, with their path
        Vars.Log.LogWarning("No right arm found to hide on the shotgun; its parts: "
            + string.Join(", ", __instance.GetComponentsInChildren<Transform>(true).Where(T => Depth(T, __instance.transform) <= 3).Select(T => T.name))
            + ". Meshes: " + string.Join(", ", __instance.GetComponentsInChildren<Renderer>(true).Select(R => PathOf(R.transform, __instance.transform) + " (" + R.GetType().Name + ")"))
            + ". Arm or hand parts: " + string.Join(", ", __instance.GetComponentsInChildren<Transform>(true)
                .Where(T => T.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0 || T.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(T => PathOf(T, __instance.transform))));
    }
    private static string PathOf(Transform T, Transform Root)
    {
        string P = T.name;
        for (T = T.parent; T != null && T != Root; T = T.parent) P = T.name + "/" + P;
        return P;
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
