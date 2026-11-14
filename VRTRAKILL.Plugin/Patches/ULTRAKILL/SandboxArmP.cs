using System.Linq;
using System.Reflection;
using HarmonyLib;
using Plugin.Systems;
using UnityEngine;

namespace Plugin.Patches.ULTRAKILL
{
    // An object picked up with the sandbox arm's move mode keeps its distance and turn from the gun hand, which aims
    // the arm (CameraControllerTranspiler). The game measures them from the camera when it picks the object up, so it
    // jumped by the gap between head and hand on the first frame.
    [HarmonyPatch]
    internal static class SandboxArmP
    {
        static MethodBase TargetMethod()
            => AccessTools.GetDeclaredConstructors(typeof(Sandbox.Arm.MoveMode.ManipulatedObject)).First(C => C.GetParameters().Length == 2);

        static void Postfix(Sandbox.Arm.MoveMode.ManipulatedObject __instance)
        {
            if (Vars.DominantHand == null || __instance.target == null) return;
            Transform Hand = Vars.DominantHand.transform;
            __instance.distance = Vector3.Distance(Hand.position, __instance.target.position - __instance.positionOffset);
            __instance.rotationOffset = Quaternion.Inverse(Hand.rotation) * __instance.target.rotation;
        }
    }
}
