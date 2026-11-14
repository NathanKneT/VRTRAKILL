﻿using UnityEngine;
using Valve.VR;

namespace Plugin.Systems.UI
{
    // Moves the crosshair onto the shot line, at the depth of what the gun aims at.
    internal class CrosshairController : MonoBehaviour
    {
        // Where the crosshair sits when the gun aims at nothing within reach (open sky); also the distance its size was
        // made for
        public float Length => Vars.Config.CBS.CrosshairDistance;

        // A magic number which gives us the most accurate center
        Vector3 Offset = new Vector3(-.2f, -2.75f, 0);

        // The crosshair sat at a fixed distance (8 m) on the shot line. In front of a nearer wall it floated behind the
        // wall, and in front of a shop screen 1 m away the eyes, which are not where the hand is, saw it over another
        // button than the one the hand pointed at (PS VR2 test). It now sits on what the shot line hits, scaled so it
        // keeps the same size to the eye, and eased so it does not jump between near and far edges.
        private const float Reach = 200f, Closest = .25f, EaseRate = 20f;
        private Vector3 BaseScale;
        private float Shown = -1f;

        // Places the crosshair ahead of the view while frozen without controller UI, else on the shot line.
        // Every gun fires from the gun hand along its aim: CameraControllerTranspiler and PatchRocketLauncher send the
        // shot code's camera reads to Vars.DominantHand. The crosshair used to be placed from GunControl's transform, or
        // from the weapon model for the rocket launcher and jackhammer, which sit apart from the hand: measured in Level
        // 1-1, it was 3 degrees off the shot line for every gun and 5.6 for the rocket launcher, while the rocket itself
        // left 0.00 degrees off the hand's aim (issues #165, #195). Placing it from the gun hand puts it on the line.
        public void LateUpdate()
        {
            if (BaseScale == Vector3.zero) BaseScale = transform.localScale;
            bool Laser = Vars.IsPlayerFrozen || Vars.IsPlayerUsingShop;
            if (Laser && !Vars.Config.UIInteraction.ControllerBased)
            {
                transform.localScale = BaseScale;
                transform.position = Vars.MainCamera.transform.position + (Vars.MainCamera.transform.forward * Length * .25f) + Offset;
                return;
            }

            Transform Aim = Vars.DominantHand.transform;
            float Target = AimDistance(Aim, Laser);
            float Step = 1f - Mathf.Exp(-EaseRate * Time.unscaledDeltaTime);
            Shown = Shown < 0f ? Target : Mathf.Lerp(Shown, Target, Step);

            transform.position = Aim.position + Aim.forward * Shown;
            transform.rotation = Aim.rotation;
            // the size was made for Length; scaling with the distance keeps the same size to the eye
            transform.localScale = BaseScale * (Shown / Mathf.Max(Length, .01f));
        }

        // How far along the shot line the crosshair goes: the laser's hit on a menu or shop screen when the gun hand owns
        // the laser, else the first enemy or wall the shot line hits, else Length.
        private float AimDistance(Transform Aim, bool Laser)
        {
            SteamVR_Input_Sources GunHand = Vars.Config.Controllers.LeftHanded ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            if (Laser && UIInteraction.HasHit && UIInteraction.ActiveHand == GunHand)
                return Mathf.Max(Closest, Vector3.Dot(UIInteraction.HitPoint - Aim.position, Aim.forward));
            if (Physics.Raycast(Aim.position, Aim.forward, out RaycastHit Hit, Reach, LayerMaskDefaults.Get(LMD.EnemiesAndEnvironment), QueryTriggerInteraction.Ignore))
                return Mathf.Max(Closest, Hit.distance);
            return Length;
        }
    }
}
