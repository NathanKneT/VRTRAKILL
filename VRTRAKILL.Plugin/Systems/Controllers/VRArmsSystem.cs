using UnityEngine;
using Valve.VR;
using VRBasePlugin.Systems.VRCamera;

namespace Plugin.Systems.Controllers
{
    public class VRArmsSystem : MonoSingleton<VRArmsSystem>
    {
        public VRControllersSystem CC;
        public GameObject GunOffset;

        public Vector3 ArmIKOffset = new Vector3(0, .05f, -.11f);

        private Vector3 _PreviousPosition; private bool _HasPreviousPosition; public float Speed = 0; // for punch detection
        // the fastest hand speed of the last PunchWindow seconds: a punch press that comes a few frames after the
        // swing's peak (or a swing that peaks between frames) still counts, instead of needing both on the same frame
        public float PeakSpeed = 0; private float _PeakTime;
        // the hand's velocity at that peak, in tracking space (m/s): the direction of a throw
        public Vector3 PeakVelocity;
        private const float PunchWindow = 0.15f;
        internal static float? TestHandSpeed; // set only by the developer test hook (DevTestHooks)
        private Vector3 LastPosition; // for direction
        // Measures the hand speed since the last frame in tracking space, for punch detection.
        private void CalculateSpeed()
        {
            // world position and scaled time added locomotion and multiplied by the weapon wheel and hitstop time scales
            Vector3 Position = CC.source == SteamVR_Input_Sources.LeftHand ? VRControllerLocations.Instance.leftPos : VRControllerLocations.Instance.rightPos;
            float Delta = Time.unscaledDeltaTime;
            // a hand that jumps back from where it was held during lost tracking is not a swing
            if (VRControllerLocations.Instance.HandJumped(CC.source)) _HasPreviousPosition = false;
            Vector3 Velocity = Vector3.zero;
            if (_HasPreviousPosition && Delta > 0f) { Velocity = (Position - _PreviousPosition) / Delta; Speed = Velocity.magnitude; }
            _PreviousPosition = Position; _HasPreviousPosition = true;
            if (TestHandSpeed.HasValue) Speed = TestHandSpeed.Value; // developer test hook only (DevTestHooks)

            float Now = Time.unscaledTime;
            if (Speed >= PeakSpeed || Now - _PeakTime > PunchWindow) { PeakSpeed = Speed; PeakVelocity = Velocity; _PeakTime = Now; }
        }

        // Gets the controller and gun offset, and puts the UI event camera on the hand for controller based UI.
        public void Awake()
        {
            CC = gameObject.GetComponent<VRControllersSystem>();
            GunOffset = CC.GunOffset;
            LastPosition = transform.position;

            if (Vars.Config.UIInteraction.ControllerBased)
            {
                // local zero keeps the ray on the hand wherever the hand is at creation
                UI.UIConverter.UIEventCamera.transform.SetParent(GunOffset.transform, false);
                UI.UIConverter.UIEventCamera.transform.localPosition = Vector3.zero;
                UI.UIConverter.UIEventCamera.transform.localRotation = Quaternion.identity;
            }
        }

        // Sets the arm IK offset, measures hand speed, updates the punch direction and toggles the controller model.
        public void Update()
        {
            CC.ArmOffset.transform.localPosition = ArmIKOffset;

            CalculateSpeed();

            if (LastPosition != transform.position)
            {
                Vars.punchDirection = Vars.NonDominantHand.transform.forward;
                LastPosition = transform.position;
            }

            SetControllers();
        }

        // Shows the controller model when no arms are spawned or in the main menu, if the controller has a model.
        private void SetControllers()
        {
            if (CC.RenderModel == null) return;
            FistControl Fists = Vars.Registered<FistControl>();
            bool NoArms = Fists != null && Fists.spawnedArms.Count == 0;
            if ((NoArms && !Vars.IsPlayerFrozen) || (NoArms && !Fists.activated) || Vars.IsMainMenu)
                CC.RenderModel.SetActive(true);
            else CC.RenderModel.SetActive(false);
        }
    }
}
