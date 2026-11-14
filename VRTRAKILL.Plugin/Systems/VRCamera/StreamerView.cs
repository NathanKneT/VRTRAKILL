using UnityEngine;
using UnityEngine.XR;

namespace Plugin.Systems.VRCamera
{
    // The streamer view: what the PC monitor shows while you play, made to be recorded or streamed. The plain mirror is
    // one eye of the headset: every small head movement shakes it, it rolls with the head, and its shape is the lens's.
    // This view is its own camera at the middle of the eyes, smoothed the way spectator cameras in VR games are:
    //  - head turns are smoothed with a half-life ("Turn smoothing": Off 0, Low 0.05 s, Medium 0.09 s, High 0.15 s),
    //    shortened during fast turns so a quick look stays readable, and head position with 0.06 s;
    //  - only the head is smoothed, against the player body, so the game's own motion (dash, slide, grapple) is not
    //    delayed; snap turns and gravity turns ease over a few frames for the viewer;
    //  - the horizon is levelled ("Level horizon", 1 = always level), fading out when looking straight up or down;
    //  - a horizontal field of view (90 degrees by default) on a 16:9 picture, letterboxed to the window;
    //  - rendered at "Frame rate" (60 by default: every second frame at 120 Hz) into a picture of "Height in pixels",
    //    so the headset keeps its frame rate. The monitor shows the last picture every frame.
    // The comfort vignette and the low health glow belong to the eyes and are hidden from it. StreamerOverlay shows the
    // picture on the monitor, with a HUD and the gaze, parry and trigger overlays for videos. The toggle key ("Toggle
    // streamer view", T) and "STREAMER VIEW" in the VR column turn it on and off; the choice is saved. Off, the monitor
    // shows the headset mirror as before.
    [DefaultExecutionOrder(1000)] // after CameraController (eyes placed) and PortalManagerV2 (999)
    internal sealed class StreamerView : MonoBehaviour
    {
        public static StreamerView Instance { get; private set; }
        public static Camera WorldCamera => Instance != null ? Instance.World : null;
        // the last picture, shown on the monitor by StreamerOverlay
        internal static RenderTexture Picture => Instance != null ? Instance.Target : null;
        public static bool Showing => Instance != null && Instance.Target != null && Vars.Config.StreamerView.On && XRSettings.isDeviceActive;

        private Camera World, UI;
        private Camera MadeFor;
        private RenderTexture Target;
        private bool Smoothed;
        private Vector3 LocalPos;
        private Quaternion LocalRot, Rig, LastLocalTarget;
        private float Accumulated;

        public static void Create(GameObject Host)
        {
            if (Instance == null) Instance = Host.AddComponent<StreamerView>();
            StreamerOverlay.Create();
        }

        public static void Toggle()
        {
            Vars.Config.StreamerView.On = !Vars.Config.StreamerView.On;
            try { Prefs.ConfigJSON.Serialize(Prefs.ConfigJSON.Instance); }
            catch (System.Exception E) { Vars.Log.LogWarning("The streamer view setting could not be saved: " + E.Message); }
            Vars.Log.LogInfo("Streamer view " + (Vars.Config.StreamerView.On ? "on." : "off."));
        }

        private void Update()
        {
            if (Prefs.ConfigMaster.ToggleStreamerView is KeyCode Key && UnityEngine.Input.GetKeyDown(Key)) Toggle();
        }

        private void LateUpdate()
        {
            bool Want = Vars.Config.StreamerView.On && XRSettings.isDeviceActive;
            if (!Want) { Smoothed = false; return; }
            Camera L = Patches.CameraConverterP.leftEye, R = Patches.CameraConverterP.rightEye;
            if (L == null || R == null) return;
            if (MadeFor != L || World == null) MakeCameras(L);
            // only on frames the eyes were placed: a loading level holds its last picture
            if (Patches.CameraConverterP.EyesPlacedFrame != Time.frameCount) return;
            Place(L, R);
            if (!Due()) return;
            MakeTarget();
            Draw(L);
        }

        // ---- Cameras: a world camera and a UI camera on top, both rendered by hand into one picture ----
        private void MakeCameras(Camera L)
        {
            if (World != null) Destroy(World.gameObject);
            MadeFor = L;
            World = new GameObject("VRTRAKILL Streamer Camera", typeof(Camera)).GetComponent<Camera>();
            VRTRAKILL.Utilities.Unity.CopyCameraValues(World, L);
            World.transform.SetParent(L.transform.parent, false); // goes away with the eyes at a scene change
            World.stereoTargetEye = StereoTargetEyeMask.None;
            World.enabled = false;
            World.allowMSAA = false;

            UI = new GameObject("VRTRAKILL Streamer UI Camera", typeof(Camera)).GetComponent<Camera>();
            UI.transform.SetParent(World.transform, false);
            UI.stereoTargetEye = StereoTargetEyeMask.None;
            UI.enabled = false;
            UI.clearFlags = CameraClearFlags.Depth;
            UI.allowMSAA = false;
            Smoothed = false;
        }

        private void MakeTarget()
        {
            int H = Mathf.Clamp(Vars.Config.StreamerView.Height, 360, 2160);
            int W = Mathf.RoundToInt(H * 16f / 9f);
            if (Target != null && Target.width == W && Target.height == H) return;
            if (Target != null) { Target.Release(); Destroy(Target); }
            Target = new RenderTexture(W, H, 24) { name = "VRTRAKILL Streamer View" };
            Target.Create();
        }

        private bool Due()
        {
            int Rate = Vars.Config.StreamerView.FrameRate;
            if (Rate <= 0) return true;
            float Interval = 1f / Rate;
            Accumulated += Time.unscaledDeltaTime;
            if (Accumulated + .002f < Interval) return false;
            Accumulated = Mathf.Min(Accumulated - Interval, Interval);
            return true;
        }

        private void Draw(Camera L)
        {
            World.nearClipPlane = L.nearClipPlane; World.farClipPlane = L.farClipPlane;
            World.backgroundColor = L.backgroundColor; World.clearFlags = L.clearFlags; World.cullingMask = L.cullingMask;
            float Vertical = 2f * Mathf.Atan(Mathf.Tan(Mathf.Clamp(Vars.Config.StreamerView.FieldOfView, 30f, 150f) * .5f * Mathf.Deg2Rad) * 9f / 16f) * Mathf.Rad2Deg;
            World.fieldOfView = UI.fieldOfView = Vertical;
            World.aspect = UI.aspect = 16f / 9f;
            if (Vars.UICamera != null) { UI.cullingMask = Vars.UICamera.cullingMask; UI.farClipPlane = Vars.UICamera.farClipPlane; }
            UI.nearClipPlane = .1f;
            World.targetTexture = UI.targetTexture = Target;

            // the vignette ring sits in front of the eyes; it is theirs, not the viewers'
            Renderer Ring = ComfortVignette.Ring;
            bool RingWas = Ring != null && Ring.enabled;
            if (Ring != null) Ring.enabled = false;
            try { World.Render(); UI.Render(); }
            finally { if (Ring != null) Ring.enabled = RingWas; }
        }

        // ---- Pose ----
        private static float Alpha(float Dt, float HalfLife) => HalfLife <= 0f ? 1f : 1f - Mathf.Pow(.5f, Dt / HalfLife);

        private float TurnHalfLife()
        {
            switch (Vars.Config.StreamerView.TurnSmoothing)
            {
                case "Off": return 0f;
                case "Low": return .05f;
                case "High": return .15f;
                default: return .09f;
            }
        }

        private void Place(Camera L, Camera R)
        {
            CameraController CC = Vars.Registered<CameraController>();
            if (CC == null || CC.transform.parent == null) return;
            Vector3 Head = (L.transform.position + R.transform.position) * .5f;
            Quaternion HeadRot = L.transform.rotation;
            Quaternion RigNow = Patches.CameraConverterP.TrackingToWorld();
            Vector3 Body = CC.transform.parent.position;
            Vector3 PosTarget = Quaternion.Inverse(RigNow) * (Head - Body);
            Quaternion RotTarget = Quaternion.Inverse(RigNow) * HeadRot;

            float Dt = Time.unscaledDeltaTime;
            if (!Smoothed || (PosTarget - LocalPos).sqrMagnitude > 1f) // first frame, a recenter or a teleport of the head
            {
                LocalPos = PosTarget; LocalRot = RotTarget; Rig = RigNow; LastLocalTarget = RotTarget; Smoothed = true;
            }
            // shorter during fast turns: below 100 deg/s fully smoothed, from 400 deg/s at a third
            float Speed = Dt > 0f ? Quaternion.Angle(LastLocalTarget, RotTarget) / Dt : 0f;
            LastLocalTarget = RotTarget;
            float HalfLife = TurnHalfLife() * Mathf.Lerp(.3f, 1f, Mathf.InverseLerp(400f, 100f, Speed));
            LocalRot = Quaternion.Slerp(LocalRot, RotTarget, Alpha(Dt, HalfLife));
            LocalPos = Vector3.Lerp(LocalPos, PosTarget, Alpha(Dt, .06f));
            Rig = Quaternion.Slerp(Rig, RigNow, Alpha(Dt, .05f));

            Quaternion Rot = Rig * LocalRot;
            Vector3 Up = Rig * Vector3.up, Forward = Rot * Vector3.forward;
            float Pitch = 90f - Vector3.Angle(Forward, Up);
            float Level = Mathf.Clamp01(Vars.Config.StreamerView.LevelHorizon) * Mathf.InverseLerp(85f, 70f, Mathf.Abs(Pitch));
            if (Level > 0f) Rot = Quaternion.Slerp(Rot, Quaternion.LookRotation(Forward, Up), Level);

            World.transform.SetPositionAndRotation(Body + Rig * LocalPos, Rot);
            if (Patches.CameraConverterP.EyesThroughPortals) Patches.CameraConverterP.MoveFromPlayerThroughPortals(World.transform);
        }



        private void OnDestroy()
        {
            if (Target != null) { Target.Release(); Destroy(Target); }
        }
    }
}
