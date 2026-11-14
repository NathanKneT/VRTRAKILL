using UnityEngine;
using UnityEngine.XR;

namespace Plugin.Systems.Movement
{
    // Optional movement gestures, each on its own setting ("Movement gestures" in the config, GESTURES in the VR column).
    // Every one presses or releases the same game key as its button, through the mod's virtual input, and the button keeps
    // working beside it (the key is down while either asks for it). From the VR mechanics research
    // (docs: "Mecaniques VR pour ULTRAKILL"):
    //  - Slam with fists (Blade & Sorcery's ground strike): in the air, both fists driven down below the chest press slide,
    //    which the game turns into a ground slam; keeping them low after landing holds it for the power slam.
    //  - Pull the whiplash by yanking (Half-Life: Alyx's gravity gloves): while the hook has caught something, the arm hand
    //    pulled back toward the body releases the whiplash key, which is when the game pulls, even with the button held.
    //  - Slide by crouching (Sairento, Hellsweeper): on the ground, the head going down by "Crouch depth" below its standing
    //    height holds slide; standing back up releases it. Not when playing seated.
    // Positions and speeds are in tracking space, real metres, so a gesture means the same at any game speed.
    internal static class MovementGestures
    {
        // developer test hook only (DevTestHooks): pretends the head is this much lower than it is
        internal static float TestHeadDrop;
        internal static float? TestStandingHeight; // and that the standing height is this (a headset resting on a desk reads as seated)

        private static Vector3 LastLeft, LastRight; private static bool HavePositions;
        private static Vector3 LeftVelocity, RightVelocity;
        private static bool Slamming; private static float SlamPressedAt, LandedAt;
        private static bool Crouching;
        private static int Logged;

        public static void Tick()
        {
            var G = Vars.Config.Gestures;
            bool Any = G.SlamWithFists || G.YankWhiplash || G.CrouchSlide;
            NewMovement NM = Vars.Registered<NewMovement>();
            VRBasePlugin.Systems.VRCamera.VRControllerLocations Hands = VRBasePlugin.Systems.VRCamera.VRControllerLocations.Instance;
            bool Playing = Any && NM != null && !NM.dead && Hands != null && XRSettings.isDeviceActive && !Vars.IsMainMenu && !Vars.IsPaused;
            if (!Playing) { Release(); HavePositions = false; return; }

            float Dt = Time.unscaledDeltaTime;
            Vector3 Left = Hands.leftPos, Right = Hands.rightPos;
            if (HavePositions && Dt > 0f) { LeftVelocity = (Left - LastLeft) / Dt; RightVelocity = (Right - LastRight) / Dt; }
            else LeftVelocity = RightVelocity = Vector3.zero;
            LastLeft = Left; LastRight = Right; HavePositions = true;
            Vector3 Head = InputTracking.GetLocalPosition(XRNode.Head) + Vector3.down * TestHeadDrop;
            bool OnGround = NM.gc != null && NM.gc.onGround;

            Slam(G, NM, Head, Left, Right, OnGround);
            Yank(G, Head);
            Crouch(G, Head, OnGround);
            Input.SVRActionsManager.SetSlideGesture(Slamming || Crouching);
        }

        // ---- Slam with fists ----
        private static void Slam(Prefs.NewConfig._Gestures G, NewMovement NM, Vector3 Head, Vector3 Left, Vector3 Right, bool OnGround)
        {
            float Now = Time.unscaledTime;
            // fists at chest height or lower: 25 cm under the head
            bool LeftLow = Left.y < Head.y - .25f, RightLow = Right.y < Head.y - .25f;
            if (!G.SlamWithFists) { Slamming = false; return; }
            if (!Slamming)
            {
                float Down = Mathf.Min(-LeftVelocity.y, -RightVelocity.y); // the slower of the two fists
                if (!OnGround && !NM.sliding && LeftLow && RightLow && Down > G.SlamSpeed)
                {
                    Slamming = true; SlamPressedAt = Now; LandedAt = -1f;
                    Note($"Gesture: slam (both fists {Down:0.0} m/s down, in the air).");
                }
                return;
            }
            if (OnGround && LandedAt < 0f) LandedAt = Now;
            // the game refuses a slam close to the ground or right after another one: let go at once, or the key held
            // into the landing would start a slide nobody asked for
            if (Now - SlamPressedAt > .1f && LandedAt < 0f && !NM.gc.heavyFall) { Slamming = false; return; }
            // held after landing while the fists stay low: the power slam grows with the hold, up to 1 s
            if (LandedAt >= 0f && (!(LeftLow || RightLow) || Now - LandedAt > 1f)) Slamming = false;
        }

        // ---- Pull the whiplash by yanking ----
        private static void Yank(Prefs.NewConfig._Gestures G, Vector3 Head)
        {
            if (!G.YankWhiplash) return;
            HookArm Hook = Vars.Registered<HookArm>();
            if (Hook == null || Hook.state != HookState.Caught) return;
            bool ArmIsLeft = !Vars.Config.Controllers.LeftHanded;
            Vector3 Hand = ArmIsLeft ? LastLeft : LastRight, Velocity = ArmIsLeft ? LeftVelocity : RightVelocity;
            // speed toward the body, across the floor: a pull on the rope, not a punch or a raised hand
            Vector3 ToBody = Head - Hand; ToBody.y = 0f;
            if (ToBody.sqrMagnitude < .0001f) return;
            Vector3 Flat = Velocity; Flat.y = 0f;
            float Toward = Vector3.Dot(Flat, ToBody.normalized);
            if (Toward > G.YankSpeed && Input.SVRActionsManager.YankWhiplash())
                Note($"Gesture: whiplash pulled by a yank ({Toward:0.0} m/s toward the body).");
        }

        // ---- Slide by crouching ----
        private static void Crouch(Prefs.NewConfig._Gestures G, Vector3 Head, bool OnGround)
        {
            float Standing = TestStandingHeight ?? VRCamera.Patches.CameraConverterP.StandingHeadHeight;
            // off when not standing: no calibration yet, or a seated player (head under 1.3 m at calibration)
            if (!G.CrouchSlide || float.IsNaN(Standing) || Standing < 1.3f) { Crouching = false; return; }
            float Depth = Mathf.Clamp(G.CrouchDepth, .15f, .8f);
            if (!Crouching)
            {
                // starts only on the ground: a crouch in the air is not a slam (the slam has its own gesture)
                if (OnGround && Head.y < Standing - Depth)
                {
                    Crouching = true;
                    Note($"Gesture: slide (head {Standing - Head.y:0.00} m below standing).");
                }
            }
            else if (Head.y > Standing - Depth + .1f) Crouching = false; // 10 cm of hysteresis: no flicker at the edge
        }

        private static void Release()
        {
            Slamming = Crouching = false;
            Input.SVRActionsManager.SetSlideGesture(false);
        }

        private static void Note(string Text)
        {
            if (Logged++ < 20) Vars.Log.LogInfo(Text);
        }
    }
}
