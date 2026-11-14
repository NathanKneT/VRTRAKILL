using HarmonyLib;
using Plugin.Systems;
using UnityEngine;

namespace Plugin.Patches.ULTRAKILL;

[HarmonyPatch(typeof(Revolver))]
internal class PatchRevolver
{
    // Replaces ThrowCoin to throw coins from the arm hand, the gun hand or the camera, based on the aim settings.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Revolver.ThrowCoin))]
    private static bool ThrowCoin(Revolver __instance)
    {
        // the arm hand throws it with swing punching, and whenever the coin follows the throw; else the gun hand
        __instance.camObj = Vars.Config.EnableMBP || Vars.Config.MBP.CoinThrow ? Vars.NonDominantHand : Vars.DominantHand;

        // rest of the game's ThrowCoin (build 22957324), spawning from the camObj chosen above
        if (__instance.punch == null || !__instance.punch.gameObject.activeInHierarchy)
            __instance.punch = MonoSingleton<FistControl>.Instance.currentPunch;
        if (__instance.punch)
            __instance.punch.CoinFlip();
        MonoSingleton<PlayerAnimations>.Instance?.CoinToss();
        // the game drops the coin 0.5 below the eye; from a hand it spawns at the hand
        Coin component = Object.Instantiate(__instance.coin, __instance.camObj.transform.position, __instance.camObj.transform.rotation).GetComponent<Coin>();
        component.sourceWeapon = __instance.gc.currentWeapon;
        // the coin falls along the player's custom gravity (inverted gravity areas)
        Vector3 vector = Vector3.down;
        if (__instance.nmov.rb.TryGetCustomGravity(out Vector3 gravity) && __instance.nmov.rb.GetCustomGravityMode())
            vector = gravity;
        component.rb.SetCustomGravity(vector.normalized * Physics.gravity.magnitude);
        component.rb.SetCustomGravityMode(useCustomGravity: true);
        MonoSingleton<RumbleManager>.Instance.SetVibration(RumbleProperties.CoinToss);
        component.rb.AddForce(Toss(__instance.camObj.transform, vector.normalized) + MonoSingleton<PlayerTracker>.Instance.GetPlayerVelocity(trueVelocity: true), ForceMode.VelocityChange);
        __instance.pierceCharge = 0f;
        __instance.pierceReady = false;

        return false;
    }
    // The coin's launch, before the player's own velocity is added. The game tosses it 20 m/s along the view and 15 m/s
    // up. In VR it went along the arm hand's pointing, whatever the hand did; now a toss made while the arm hand is
    // throwing (1.5 m/s or more within the last 0.15 s) flies along the throw: faster for a harder throw (20 to 35 m/s),
    // with some lift so it stays up to be shot. A still hand tosses it as before.
    private const float ThrowFrom = 1.5f, LiftOnThrow = 8f;
    private static Vector3 Toss(Transform From, Vector3 Down)
    {
        Vector3 Default = From.forward * 20f + -Down * 15f;
        if (!Vars.Config.MBP.CoinThrow) return Default;
        Systems.Controllers.VRArmsSystem Arms = Vars.Registered<Systems.Controllers.VRArmsSystem>();
        if (Arms == null || Arms.PeakVelocity.magnitude < ThrowFrom) return Default;
        Vector3 Throw = Systems.VRCamera.Patches.CameraConverterP.TrackingToWorld() * Arms.PeakVelocity;
        return Throw.normalized * Mathf.Clamp(Throw.magnitude * 8f, 20f, 35f) + -Down * LiftOnThrow;
    }

    // Moves the revolver muzzle to the VR hand before firing.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Revolver.Shoot))]
    private static void Shoot(Revolver __instance)
    {
        __instance.gunBarrel = Vars.DominantHand; // fire from the VR hand
    }
}
