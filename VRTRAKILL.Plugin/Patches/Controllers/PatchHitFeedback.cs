using System.Collections.Generic;
using HarmonyLib;
using Plugin.Systems;
using Plugin.Systems.Feedback;
using UnityEngine;

namespace Plugin.Patches.Controllers;

// VR feedback the gamepad rumble does not have: the hand that hit something feels it. A shot or punch that lands gives
// a short tick on that hand, a kill a stronger knock, and getting hurt a thud in both hands that grows with the
// damage. The game's sounds, hitstop and screen effects stay as they are; this only adds the touch.
[HarmonyPatch]
internal static class PatchHitFeedback
{
    // the game's "hitter" names for the player's attacks (EnemyIdentifier.hitter)
    private static readonly HashSet<string> ArmHitters = new HashSet<string>
    {
        "punch", "heavypunch", "criticalpunch", "ground slam", "groundslam", "hook", "whiplash", "drillpunch", "drillpunchkill", "parry",
    };
    private static readonly HashSet<string> GunHitters = new HashSet<string>
    {
        "revolver", "coin", "shotgun", "shotgunzone", "nail", "nailgun", "nailhit", "nailbombed", "saw", "sawblade", "chainsaw",
        "chainsawzone", "chainsawprojectile", "chainsawbounce", "railcannon", "harpoon", "drill", "drillhit", "cannonball",
        "cannonballed", "cannonboost", "rocketreturn", "explosion", "explosionhit", "zapper", "zapperhit", "electricity",
        "hammer", "hammerzone", "hammerhit", "hammerhitheavy", "hammerhitred", "hammerhitgreen", "hammerhityellow", "aftershock",
    };
    // heavy hits get a stronger tick
    private static readonly HashSet<string> HeavyHitters = new HashSet<string>
    {
        "heavypunch", "criticalpunch", "railcannon", "cannonball", "cannonballed", "hammerhitheavy", "hammerhitred", "ground slam", "groundslam",
    };

    // nails and saws hit many times a second; one tick per hand every 40 ms is enough
    private const float TickInterval = .04f;
    private static float NextArmTick, NextGunTick;

    private struct Before { public float Health; public bool Dead; }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyIdentifier), nameof(EnemyIdentifier.DeliverDamage))]
    static void DamagePrefix(EnemyIdentifier __instance, out Before __state)
        => __state = new Before { Health = __instance.health, Dead = __instance.dead };

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyIdentifier), nameof(EnemyIdentifier.DeliverDamage))]
    static void DamagePostfix(EnemyIdentifier __instance, Before __state)
    {
        if (!Vars.Config.Controllers.HitRumble || __state.Dead) return;
        string Hitter = __instance.hitter ?? string.Empty;
        bool Arm = ArmHitters.Contains(Hitter);
        if (!Arm && !GunHitters.Contains(Hitter)) return; // other enemies, hazards
        Haptics.Hand H = Arm ? Haptics.Hand.Arm : Haptics.Hand.Gun;
        if (__instance.dead)
        {
            Haptics.Add(H, .7f, .09f, Haptics.Buzz, "kill " + Hitter);
            return;
        }
        if (__instance.health >= __state.Health) return; // no damage (blocked, immune)
        float Now = Time.unscaledTime;
        if (Arm) { if (Now < NextArmTick) return; NextArmTick = Now + TickInterval; }
        else { if (Now < NextGunTick) return; NextGunTick = Now + TickInterval; }
        Haptics.Add(H, HeavyHitters.Contains(Hitter) ? .5f : .3f, .03f, Haptics.Tick, "hit " + Hitter);
    }

    // A parry is the game's sharpest moment: time stops for 0.25 s, the screen flashes, the camera shakes. In VR the
    // flash is now faint and the camera shake does not reach the eyes, and the game's own parry rumble was lost among the
    // others ("I don't really get feedback", PS VR2 test). A parry now has its own feel, stronger than anything else: a
    // full-strength crack in the arm hand, then a deep knock that lasts through the time stop, and an echo in the gun
    // hand. The vibrations run on real time, so they play while the game is stopped.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimeController), nameof(TimeController.ParryFlash))]
    static void Parried()
    {
        if (!Vars.Config.Controllers.HitRumble) return;
        Haptics.Add(Haptics.Hand.Arm, 1f, .05f, Haptics.Tick, "parry crack");
        Haptics.Add(Haptics.Hand.Arm, .85f, .28f, Haptics.Thud, "parry knock");
        Haptics.Add(Haptics.Hand.Gun, .4f, .18f, Haptics.Thud, "parry echo");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.GetHurt))]
    static void HurtPrefix(NewMovement __instance, out int __state) => __state = __instance.hp;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.GetHurt))]
    static void HurtPostfix(NewMovement __instance, int __state)
    {
        if (!Vars.Config.Controllers.DamageRumble) return;
        int Lost = __state - __instance.hp;
        if (Lost <= 0) return;
        Haptics.Add(Haptics.Hand.Both, Mathf.Clamp(.35f + Lost / 50f, .35f, 1f), .18f, Haptics.Thud, "hurt " + Lost);
    }
}
