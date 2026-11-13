using HarmonyLib;
using Plugin.Systems;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Valve.VR;

namespace Plugin.Patches.Controllers;

[HarmonyPatch(typeof(RumbleManager))]
internal sealed class PatchControllerHaptics
{
    // Returns the current haptic action; a cached copy would keep an action that a later SteamVR_Actions.PreInitialize replaced.
    static SteamVR_Action_Vibration HapticAction => SteamVR_Actions._default.Haptic;
    // RumbleManager.OnDisable runs at quit after OpenVR shut down (OpenVR.Input is null -> NRE in Execute)
    static bool Quitting;
    // Sets Quitting when the application starts to quit.
    static PatchControllerHaptics() { Application.quitting += () => Quitting = true; }
    // Runs the static constructor from Plugin.Awake, so the quit handler exists even if the first rumble comes during quit.
    internal static void WatchQuit() { }

    // Replaces the gamepad rumble update: drops ended vibrations and gives each hand the strongest rumble that belongs
    // to it (Systems/Feedback/Haptics mixes it with the mod's own pulses and sends it). Before, only the strongest
    // rumble of all played, on one controller: gunfire while sliding lost the slide.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RumbleManager.Update))]
    static bool Update(RumbleManager __instance)
    {
        __instance.discardedKeys.Clear();
        foreach (KeyValuePair<RumbleKey, PendingVibration> pendingVibration in __instance.pendingVibrations)
        {
            if (pendingVibration.Value.isTracking && (pendingVibration.Value.trackedObject == null || !pendingVibration.Value.trackedObject.activeInHierarchy))
                __instance.discardedKeys.Add(pendingVibration.Key);
            else if (pendingVibration.Value.IsFinished)
                __instance.discardedKeys.Add(pendingVibration.Key);
        }

        foreach (RumbleKey discardedKey in __instance.discardedKeys)
            __instance.pendingVibrations.Remove(discardedKey);

        float Left = 0f, Right = 0f;
        foreach (KeyValuePair<RumbleKey, PendingVibration> pendingVibration2 in __instance.pendingVibrations)
        {
            float Intensity = pendingVibration2.Value.Intensity;
            SteamVR_Input_Sources Source = ResolveController(pendingVibration2.Key.name);
            if (Source != SteamVR_Input_Sources.RightHand && Intensity > Left) Left = Intensity;
            if (Source != SteamVR_Input_Sources.LeftHand && Intensity > Right) Right = Intensity;
        }

        float Scale = MonoSingleton<PrefsManager>.Instance.GetFloat("totalRumbleIntensity") * Vars.Config.Controllers.RumbleStrength;
        if ((bool)MonoSingleton<OptionsManager>.Instance && MonoSingleton<OptionsManager>.Instance.paused)
            Scale = 0f;
        Systems.Feedback.Haptics.SetGameRumble(Mathf.Clamp01(Left * Scale), Mathf.Clamp01(Right * Scale));
        return false;
    }
    // Stops controller vibration; StopVibration and StopAllVibrations then run so their pending vibrations are removed.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RumbleManager.OnDisable))]
    [HarmonyPatch(nameof(RumbleManager.StopVibration))]
    [HarmonyPatch(nameof(RumbleManager.StopAllVibrations))]
    static bool DisableRumble(MethodBase __originalMethod)
    {
        Systems.Feedback.Haptics.SetGameRumble(0f, 0f);
        Vibrate(1, 0, 0, 0);
        // skipping all three left infinite rumbles (slide, whiplash, nailgun) on until the scene ended
        return __originalMethod.Name != nameof(RumbleManager.OnDisable);
    }

    // Sends a haptic pulse to a controller unless SteamVR input is not ready or the game is quitting.
    public static void Vibrate(float Duration, float Frequency, float Amplitude, SteamVR_Input_Sources Source)
    {
        if (Quitting || !SteamVR_Input.initialized) return;
        HapticAction.Execute(0, Duration, Frequency, Amplitude, Source);
    }

    // Returns the controller a rumble key belongs to: the arm hand, the gun hand or both.
    public static SteamVR_Input_Sources ResolveController(string Key)
    {
        switch (Key)
        {
            case "rumble.slide":
            case "rumble.dash":
            case "rumble.fall_impact":
            case "rumble.jump":
            case "rumble.fall_impact_heave":
            case "rumble.weapon_wheel_tick":
                return SteamVR_Input_Sources.Any;

            case "rumble.punch":
            case "rumble.parry_flash":
            case "rumble.coin_toss":
            case "rumble.whiplash.throw":
            case "rumble.whiplash.pull":
                // the hands PatchControllerAdder gives the arms and the guns; Vars.NDHC would auto-create a VRArmsSystem with no hand
                return Vars.Config.Controllers.LeftHanded ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;

            case "rumble.gun.fire":
            case "rumble.gun.fire_strong":
            case "rumble.gun.fire_projectiles":
            case "rumble.gun.railcannon_idle":
            case "rumble.gun.nailgun_fire":
            case "rumble.gun.super_saw":
            case "rumble.gun.shotgun_charge":
            case "rumble.gun.sawblade":
            case "rumble.gun.revolver_charge":
            case "rumble.magnet_released":
                return Vars.Config.Controllers.LeftHanded ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;

            default:
                Debug.LogError("No intensity found for key: " + Key);
                return SteamVR_Input_Sources.Any;
        }
    }
}
