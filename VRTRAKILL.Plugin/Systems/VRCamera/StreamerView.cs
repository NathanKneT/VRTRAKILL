using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    [DefaultExecutionOrder(1000)] // meant to run after CameraController and PortalManagerV2 (999); not guaranteed for a mod
    internal sealed class StreamerView : MonoBehaviour
    {
        public static StreamerView Instance { get; private set; }
        public static Camera WorldCamera => Instance != null ? Instance.World : null;
        // the last picture, shown on the monitor by StreamerOverlay
        internal static RenderTexture Picture => Instance != null ? Instance.Target : null;
        // the raw headset view for the comparison ("Headset comparison" not Off), or null
        internal static RenderTexture HeadsetPicture => Instance != null && Comparing ? Instance.HeadsetTarget : null;
        private static bool Comparing => Vars.Config.StreamerView.Comparison == "Corner" || Vars.Config.StreamerView.Comparison == "Side by side";
        public static bool Showing => Instance != null && Instance.Target != null && Vars.Config.StreamerView.On && XRSettings.isDeviceActive;

        private Camera World, UI;
        private Camera MadeFor;
        private RenderTexture Target, HeadsetTarget;
        private bool Smoothed;
        private Vector3 LocalPos;
        private Quaternion LocalRot, Rig, LastLocalTarget;
        private float Accumulated;

        public static void Create(GameObject Host)
        {
            if (Instance == null) Instance = Host.AddComponent<StreamerView>();
            StreamerOverlay.Create(Host);
        }

        public static void Toggle()
        {
            Vars.Config.StreamerView.On = !Vars.Config.StreamerView.On;
            try { Prefs.ConfigJSON.Serialize(Prefs.ConfigJSON.Instance); }
            catch (System.Exception E) { Vars.Log.LogWarning("The streamer view setting could not be saved: " + E.Message); }
            Vars.Log.LogInfo("Streamer view " + (Vars.Config.StreamerView.On ? "on." : "off."));
        }

        // Off -> Corner -> Side by side -> Off ("Headset comparison" key, Y, and COMPARE in the VR column); saved
        public static void CycleComparison()
        {
            var S = Vars.Config.StreamerView;
            S.Comparison = S.Comparison == "Corner" ? "Side by side" : S.Comparison == "Side by side" ? "Off" : "Corner";
            try { Prefs.ConfigJSON.Serialize(Prefs.ConfigJSON.Instance); }
            catch (System.Exception E) { Vars.Log.LogWarning("The headset comparison setting could not be saved: " + E.Message); }
            Vars.Log.LogInfo("Streamer view headset comparison: " + S.Comparison + ".");
        }

        private void Update()
        {
            if (Prefs.ConfigMaster.ToggleStreamerView is KeyCode Key && UnityEngine.Input.GetKeyDown(Key)) Toggle();
            if (Prefs.ConfigMaster.CycleComparison is KeyCode Compare && UnityEngine.Input.GetKeyDown(Compare)) CycleComparison();
        }

        private void LateUpdate()
        {
            StreamerOverlay.KeepAlive();
            bool Want = Vars.Config.StreamerView.On && XRSettings.isDeviceActive;
            if (!Want) { Smoothed = false; return; }
            Camera L = Patches.CameraConverterP.leftEye, R = Patches.CameraConverterP.rightEye;
            if (L == null || R == null) return;
            if (MadeFor != L || World == null) MakeCameras(L);
            // only while the eyes are being placed (this frame or the last: Unity may run this LateUpdate before the
            // camera's, since [DefaultExecutionOrder] is not applied to a mod's scripts); a loading level holds its picture
            if (Time.frameCount - Patches.CameraConverterP.EyesPlacedFrame > 1) return;
            Place(L, R);
            if (!Due()) return;
            MakeTarget();
            Draw(L, Target);
            if (Comparing) DrawHeadset(L, R);
        }

        // The comparison picture, made to look like a plain recording of the headset (the SteamVR mirror in a 16:9
        // window): the left eye's own position and rotation, so it shakes and rolls with the head, and the eye's own
        // horizontal field of view cropped to 16:9, with what the eye sees in front of it (the comfort vignette, the
        // game's style panel). Half the height of the streamer picture to keep the cost down. The cameras go back to the
        // smoothed pose afterwards.
        private void DrawHeadset(Camera L, Camera R)
        {
            int H = Mathf.Clamp(Vars.Config.StreamerView.Height / 2, 360, 1080), W = Mathf.RoundToInt(H * 16f / 9f);
            if (HeadsetTarget == null || HeadsetTarget.height != H)
            {
                if (HeadsetTarget != null) { HeadsetTarget.Release(); Destroy(HeadsetTarget); }
                HeadsetTarget = new RenderTexture(W, H, 24) { name = "VRTRAKILL Streamer Headset Comparison" };
                HeadsetTarget.Create();
            }
            Transform T = World.transform;
            Vector3 Smooth = T.position; Quaternion SmoothRot = T.rotation;
            T.SetPositionAndRotation(L.transform.position, L.transform.rotation);
            // the eye's horizontal half-width from its projection (m00 = 1 / tan of the half angle)
            Matrix4x4 Eye = L.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            float HalfWidth = Eye.m00 > .1f ? 1f / Eye.m00 : Mathf.Tan(50f * Mathf.Deg2Rad);
            float Vertical = 2f * Mathf.Atan(HalfWidth * 9f / 16f) * Mathf.Rad2Deg;
            try { Draw(L, HeadsetTarget, Vertical, ForViewers: false); }
            finally { T.SetPositionAndRotation(Smooth, SmoothRot); }
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

        // The game's full-screen effects (parry flash, hurt screen, fades to black or white, the underwater tint), made 10
        // times bigger so they fill the headset's view (HUDOptionsP). Seen from this camera, which is not at the eyes and
        // has a narrower view, they covered only part of the picture, in big pixel blocks (a parry showed a brown shape
        // over half the screen). The viewers' render leaves them out and the monitor HUD draws them over the whole picture
        // (StreamerOverlay.UpdateEffects).
        internal static readonly List<Image> ScreenEffects = new List<Image>();
        private static readonly List<Image> HiddenEffects = new List<Image>();
        internal static void AddScreenEffect(Image I)
        {
            ScreenEffects.RemoveAll(E => E == null);
            if (I != null && !ScreenEffects.Contains(I)) ScreenEffects.Add(I);
        }

        // ForViewers: hides what belongs to the player's eyes (the vignette), the game's floating style panel, which
        // the monitor HUD replaces, and the game's screen effects (ScreenEffects). VerticalFov below 0: the "Field of
        // view" setting.
        private void Draw(Camera L, RenderTexture Into, float VerticalFov = -1f, bool ForViewers = true)
        {
            World.nearClipPlane = L.nearClipPlane; World.farClipPlane = L.farClipPlane;
            World.backgroundColor = L.backgroundColor; World.clearFlags = L.clearFlags; World.cullingMask = L.cullingMask;
            float Vertical = VerticalFov > 0f ? VerticalFov
                : 2f * Mathf.Atan(Mathf.Tan(Mathf.Clamp(Vars.Config.StreamerView.FieldOfView, 30f, 150f) * .5f * Mathf.Deg2Rad) * 9f / 16f) * Mathf.Rad2Deg;
            World.fieldOfView = UI.fieldOfView = Vertical;
            World.aspect = UI.aspect = 16f / 9f;
            if (Vars.UICamera != null) { UI.cullingMask = Vars.UICamera.cullingMask; UI.farClipPlane = Vars.UICamera.farClipPlane; }
            UI.nearClipPlane = .1f;
            World.targetTexture = UI.targetTexture = Into;

            // the vignette ring sits in front of the eyes; it is theirs, not the viewers'
            Renderer Ring = ForViewers ? ComfortVignette.Ring : null;
            bool RingWas = Ring != null && Ring.enabled;
            if (Ring != null) Ring.enabled = false;
            // The game's style panel floats beside the view in VR and covered a good part of the picture, next to the
            // monitor HUD's own rank, meter and bonus list. While that HUD shows, the panel is moved far below for this
            // render only (a world space canvas is drawn where its transform is, with no rebuild).
            Transform Style = ForViewers && Vars.Config.StreamerView.Hud != "Off" ? StylePanel() : null;
            Vector3 StyleAt = Style != null ? Style.position : default;
            if (Style != null) Style.position = StyleAt + Vector3.down * 100000f;
            HiddenEffects.Clear();
            if (ForViewers)
                foreach (Image E in ScreenEffects)
                    if (E != null && E.enabled) { E.enabled = false; HiddenEffects.Add(E); }
            try { World.Render(); UI.Render(); }
            finally
            {
                if (Ring != null) Ring.enabled = RingWas;
                if (Style != null) Style.position = StyleAt;
                foreach (Image E in HiddenEffects) if (E != null) E.enabled = true;
                HiddenEffects.Clear();
            }
        }

        // The canvas of the game's style HUD (StyleCanvas, converted to world space for VR), or null. Only a canvas that
        // holds nothing but the style HUD is moved, so a different layout never takes other UI out of the picture.
        // Looked for again once a second until found: the canvas is converted to world space when its HUDPos first runs.
        private StyleHUD StyleSource; private Transform StyleCanvas; private float StyleCheckedAt; private bool StyleLogged;
        private Transform StylePanel()
        {
            StyleHUD S = Vars.Registered<StyleHUD>();
            if (S != StyleSource) { StyleSource = S; StyleCanvas = null; StyleCheckedAt = -10f; StyleLogged = false; }
            if (S == null || StyleCanvas != null || Time.unscaledTime - StyleCheckedAt < 1f) return StyleCanvas;
            StyleCheckedAt = Time.unscaledTime;
            Canvas C = S.GetComponentInParent<Canvas>();
            C = C != null ? C.rootCanvas : null;
            if (C == null || C.renderMode != RenderMode.WorldSpace) return null;
            bool OnlyStyle = C.GetComponent<HUDPos>() != null && C.GetComponentsInChildren<HUDPos>(true).Length == 1;
            if (OnlyStyle) StyleCanvas = C.transform;
            if (!StyleLogged)
            {
                StyleLogged = true;
                Vars.Log.LogInfo(OnlyStyle ? $"Streamer view: the game's style panel ({C.name}) is left out of the picture; the monitor HUD shows the rank and bonuses."
                                           : $"Streamer view: the game's style panel stays in the picture (its canvas {C.name} holds other UI).");
            }
            return StyleCanvas;
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
            // the first frame of a level change can still see half torn-down portal data
            if (Patches.CameraConverterP.EyesThroughPortals)
                try { Patches.CameraConverterP.MoveFromPlayerThroughPortals(World.transform); } catch (System.NullReferenceException) { }
        }



        private void OnDestroy()
        {
            if (Target != null) { Target.Release(); Destroy(Target); }
            if (HeadsetTarget != null) { HeadsetTarget.Release(); Destroy(HeadsetTarget); }
        }
    }
}
