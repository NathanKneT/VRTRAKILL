using Plugin.Systems;
using Plugin.Systems.Input;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

namespace VRBasePlugin.Systems.VRCamera
{
    [ConfigureSingleton(SingletonFlags.PersistAutoInstance)]
    internal class VRControllerLocations : MonoSingleton<VRControllerLocations>
    {
        public Vector3 headPos;
        public Quaternion headRot = Quaternion.identity;

        public Vector3 leftPos;
        public Quaternion leftRot = Quaternion.identity;
        private Transform _left;

        public Vector3 rightPos;
        public Quaternion rightRot = Quaternion.identity;
        private Transform _right;

        // Creates the left and right SteamVR pose trackers and turns them on after their pose actions are set.
        IEnumerator Start()
        {
            transform.position = Vector3.zero;

            // this singleton persists and builds the trackers once; when VR starts late (headset turned on after the game),
            // it can be created before SteamVR input exists, and trackers built then threw in OnEnable and never tracked
            while (!SteamVR_Input.initialized) yield return null;

            SteamVR_Actions._default.Activate();
            yield return null;

            if (_left != null) GameObject.DestroyImmediate(_left.gameObject);
            _left = new GameObject("Left").transform;
            _left.SetParent(transform);
            _left.gameObject.SetActive(false); // OnEnable would bind the default "Pose" action before poseAction is set
            SteamVR_Behaviour_Pose l = _left.gameObject.AddComponent<SteamVR_Behaviour_Pose>();
            l.poseAction = SteamVR_Actions._default.LeftPose;
            l.inputSource = SteamVR_Input_Sources.LeftHand;

            if (_right != null) GameObject.DestroyImmediate(_right.gameObject);
            _right = new GameObject("Right").transform;
            _right.SetParent(transform);
            _right.gameObject.SetActive(false);
            SteamVR_Behaviour_Pose r = _right.gameObject.AddComponent<SteamVR_Behaviour_Pose>();
            r.poseAction = SteamVR_Actions._default.RightPose;
            r.inputSource = SteamVR_Input_Sources.RightHand;

            l.gameObject.SetActive(false);
            r.gameObject.SetActive(false);
            yield return null;
            l.gameObject.SetActive(true);
            r.gameObject.SetActive(true);
        }

        // ---- Lost controller tracking ----
        // PS VR2 Sense controllers are found by the headset's cameras; when the cameras lose one (hands low or close to the
        // face, Bluetooth timing), the driver goes on from the controller's motion sensors and the position drifts, once
        // 3.7 m under the floor in the PS VR2 test's SteamVR log. The rotation stays right (gyroscope). While SteamVR says
        // the pose is not valid or out of range, or the hand is farther from the head than an arm reaches, the hand keeps
        // its last good place relative to the head (and turns with the head's yaw), and the rotation stays live, so
        // aiming still works through a short loss. When tracking comes back, the hand is where it really is again.
        private struct HandMemory { public bool Has, Held; public Vector3 Offset; public float LoggedAt; public int JumpFrame; }
        private HandMemory LeftHeld, RightHeld;

        // True on the frame a hand jumped between its held place and its tracked one; hand speed must skip that frame,
        // or the jump would read as a punch.
        public bool HandJumped(SteamVR_Input_Sources Source)
            => (Source == SteamVR_Input_Sources.LeftHand ? LeftHeld.JumpFrame : RightHeld.JumpFrame) == Time.frameCount;
        private const float MaxReach = 1.2f; // metres between the head and a hand, more than any arm

        private Vector3 Guard(SteamVR_Action_Pose Action, SteamVR_Input_Sources Source, Vector3 Pos, ref HandMemory M, string Name)
        {
            if (Action == null || !SteamVR_Input.initialized) return Pos;
            ETrackingResult State = Action[Source].trackingState;
            bool Valid = Action.GetPoseIsValid(Source) && State != ETrackingResult.Running_OutOfRange && State != ETrackingResult.Calibrating_OutOfRange;
            bool InReach = (Pos - headPos).sqrMagnitude <= MaxReach * MaxReach;
            Quaternion Yaw = Quaternion.Euler(0f, headRot.eulerAngles.y, 0f);
            if (Valid && InReach)
            {
                M.Offset = Quaternion.Inverse(Yaw) * (Pos - headPos);
                M.Has = true;
                if (M.Held) { M.Held = false; M.JumpFrame = Time.frameCount; Vars.Log.LogInfo($"{Name} controller tracked again."); }
                return Pos;
            }
            if (!M.Has || !Action.GetDeviceIsConnected(Source)) return Pos;
            if (!M.Held)
            {
                M.Held = true;
                M.JumpFrame = Time.frameCount;
                if (Time.unscaledTime - M.LoggedAt > 5f)
                {
                    M.LoggedAt = Time.unscaledTime;
                    Vars.Log.LogInfo($"{Name} controller lost tracking ({(Valid ? $"{(Pos - headPos).magnitude:0.0} m from the head" : State.ToString())}); its hand stays at its last place until it is found again.");
                }
            }
            return headPos + Yaw * M.Offset;
        }

        internal static Pose? TestLeftPose, TestRightPose; // set only by the developer test hook (DevTestHooks)
        private bool IsTurning; private float SnapTurnTimer;
        // Reads the head and hand poses, updates the runtime-built actions and updates the turn offset for smooth or snap turning.
        public void Update()
        {
            headPos = InputTracking.GetLocalPosition(XRNode.Head);
            headRot = InputTracking.GetLocalRotation(XRNode.Head);
            if (_left != null)
            {
                leftPos = _left.position;
                leftRot = _left.rotation;
            }
            if (_right != null)
            {
                rightPos = _right.position;
                rightRot = _right.rotation;
            }
            leftPos = Guard(SteamVR_Actions._default.LeftPose, SteamVR_Input_Sources.LeftHand, leftPos, ref LeftHeld, "Left");
            rightPos = Guard(SteamVR_Actions._default.RightPose, SteamVR_Input_Sources.RightHand, rightPos, ref RightHeld, "Right");
            // developer test hook only (DevTestHooks): fixed hand poses in tracking space, for headsets without controllers
            if (TestLeftPose.HasValue) { leftPos = TestLeftPose.Value.position; leftRot = TestLeftPose.Value.rotation; }
            if (TestRightPose.HasValue) { rightPos = TestRightPose.Value.position; rightRot = TestRightPose.Value.rotation; }

            SVRActionsManager.UpdateRuntimeActions();

            // a mostly vertical push does not turn, so menu scrolls and Vive trackpad clicks up or down do not turn
            float TurnX = Mathf.Abs(InputVars.TurnVector.x) >= Mathf.Abs(InputVars.TurnVector.y) ? InputVars.TurnVector.x : 0;
            if (!Vars.Config.Controllers.SnapTurn)
            {
                // real time: the game's slow motion and hit stops slowed or froze the turn; never while paused
                float Dt = Vars.IsPaused ? 0f : Time.unscaledDeltaTime;
                if (TurnX > 0 + Vars.Config.Controllers.Deadzone)
                    InputVars.TurnOffset += Vars.Config.Controllers.SmoothSpeed * Dt;
                if (TurnX < 0 - Vars.Config.Controllers.Deadzone)
                    InputVars.TurnOffset -= Vars.Config.Controllers.SmoothSpeed * Dt;
            }
            else
            {
                if (IsTurning)
                {
                    SnapTurnTimer += Time.unscaledDeltaTime;
                    if (SnapTurnTimer >= .2f || TurnX == 0) { IsTurning = false; SnapTurnTimer = 0; }
                }
                else
                {
                    if (TurnX > 0 + Vars.Config.Controllers.Deadzone)
                    { IsTurning = true; InputVars.TurnOffset += Vars.Config.Controllers.SnapAngles; }
                    else if (TurnX < 0 - Vars.Config.Controllers.Deadzone)
                    { IsTurning = true; InputVars.TurnOffset -= Vars.Config.Controllers.SnapAngles; }
                }
            }
        }
    }
}
