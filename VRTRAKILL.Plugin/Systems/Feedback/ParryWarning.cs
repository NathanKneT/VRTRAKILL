using System.Collections.Generic;
using UnityEngine;

namespace Plugin.Systems.Feedback
{
    // A tick in the arm hand when an enemy projectile comes within parry reach. On a flat screen a parry is timed by
    // eye, with the projectile in the middle of the view; in VR it can come from the side or below, and the PS VR2
    // tester wanted "to feel the timing better". When a projectile the Feedbacker can parry gets within reach of the
    // punch (4 m of the arm hand, the game's parry check) and is still coming closer, the arm hand ticks once: now.
    // Each projectile ticks once, and ticks are at least 0.12 s apart, so a volley is a short patter, not a buzz. Only
    // with the Feedbacker (the Knuckleblaster cannot parry), and "Parry warning" in the controller settings turns it off.
    internal static class ParryWarning
    {
        private const float Reach = 4f, Gap = .12f;
        private const int ProjectileLayer = 1 << 14; // the layer Punch checks for parryable projectiles
        private static readonly Collider[] Found = new Collider[32];
        private static readonly HashSet<int> Warned = new HashSet<int>();
        private static float LastTick = -10f, LastClear;

        // Called once a frame, before the vibrations are sent.
        public static void Tick()
        {
            if (!Vars.Config.Controllers.ParryWarning || Vars.IsMainMenu || Vars.IsPaused) return;
            NewMovement NM = Vars.Registered<NewMovement>();
            if (NM == null || NM.dead) return;
            FistControl Fists = Vars.Registered<FistControl>();
            if (Fists == null || Fists.currentPunch == null || Fists.currentPunch.type != FistType.Standard) return;

            // forget projectiles now and then; ids are only reused long after a projectile is gone
            if (Time.unscaledTime - LastClear > 10f) { Warned.Clear(); LastClear = Time.unscaledTime; }

            // the registered arm only: Vars.NonDominantHand would create an empty arm system before the VR hands exist
            Controllers.VRArmsSystem Arms = Vars.Registered<Controllers.VRArmsSystem>();
            if (Arms == null || Arms.GunOffset == null) return;
            Vector3 Hand = Arms.GunOffset.transform.position;
            int N = Physics.OverlapSphereNonAlloc(Hand, Reach, Found, ProjectileLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < N; i++)
            {
                Collider C = Found[i];
                Projectile P = C.attachedRigidbody != null ? C.attachedRigidbody.GetComponent<Projectile>() : C.GetComponentInParent<Projectile>();
                if (P == null || P.friendly || P.parried || P.playerBullet || P.unparryable || P.undeflectable) continue;
                if (Vector3.Dot(P.transform.forward, Hand - P.transform.position) <= 0f) continue; // already going away
                if (!Warned.Add(P.GetInstanceID())) continue;
                if (Time.unscaledTime - LastTick < Gap) continue;
                LastTick = Time.unscaledTime;
                Haptics.Add(Haptics.Hand.Arm, .35f, .03f, Haptics.Tick, "parry warning");
            }
        }
    }
}
