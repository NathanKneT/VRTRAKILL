using HarmonyLib;
using Plugin.Systems.Input;
using System.Linq;
using ULTRAKILL.Portal;
using UnityEngine;
using UnityEngine.XR;
using VRBasePlugin.Systems.VRCamera;

namespace Plugin.Systems.VRCamera.Patches
{
    [HarmonyPatch]
    public class CameraConverterP
    {
        // adapted from HuskVR
        public static Camera leftEye, rightEye;
        // the frame the eyes were last placed, and whether the portal check placed them (not in 4-S); the streamer view
        // follows the eyes only on those frames: while a level loads the portal data is being torn down
        public static int EyesPlacedFrame = -1;
        public static bool EyesThroughPortals;

        // Sets up the main camera for VR and creates the left and right eye cameras; logs and returns if there is no camera yet.
        public static void SetupEyeCameras()
        {
            // PortalManagerV2.OnEnable can run before CameraController registers or sets cam (Start, CheckAspectRatio)
            CameraController cc = Vars.Registered<CameraController>() ?? Object.FindAnyObjectByType<CameraController>();
            if (cc != null && cc.cam == null) cc.cam = cc.GetComponent<Camera>();
            if (cc == null || cc.cam == null)
            {
                Vars.Log.LogWarning("No camera controller when the portal manager started; VR eye cameras not created.");
                return;
            }

            cc.cam.nearClipPlane = 0.01f;
            cc.cam.stereoTargetEye = StereoTargetEyeMask.None;

            // render the AlwaysOnTop layer (hands)
            cc.cam.cullingMask |= 1 << (int)Layers.AlwaysOnTop;
            if (cc.hudCamera != null) cc.hudCamera.enabled = false;

            leftEye = new GameObject("Left", typeof(Camera)).GetComponent<Camera>();
            VRTRAKILL.Utilities.Unity.CopyCameraValues(leftEye, cc.cam);
            leftEye.transform.SetParent(cc.transform.parent);
            leftEye.stereoTargetEye = StereoTargetEyeMask.Left;

            rightEye = new GameObject("Right", typeof(Camera)).GetComponent<Camera>();
            VRTRAKILL.Utilities.Unity.CopyCameraValues(rightEye, cc.cam);
            rightEye.transform.SetParent(cc.transform.parent);
            rightEye.stereoTargetEye = StereoTargetEyeMask.Right;
            ComfortVignette.Attach(leftEye, rightEye);

            if (PendingHeadMaterial != null) { AddHead(PendingHeadMaterial); PendingHeadMaterial = null; }
        }
        // Turns off the virtual camera and the main camera before the camera controller starts, if the camera is set.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.Start))]
        static void ConvertCameras(CameraController __instance)
        {
            // OnEnable sets cam through CheckAspectRatio; the old busy-wait here could never end on the main thread
            if (__instance.cam == null)
            {
                Vars.Log.LogWarning("CameraController.Start ran without a camera; VR camera conversion skipped.");
                return;
            }

            // deactivate, not destroy: destroying it breaks the game
            GameObject VirtualCamera = GameObject.Find("Virtual Camera");
            if (VirtualCamera != null) VirtualCamera.SetActive(false);

            __instance.cam.enabled = false;
        }
        // Restores the player rigidbody constraints and turns cc back on when a hit kills the player.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.GetHurt))]
        public static void FixWeirdDeathThing(NewMovement __instance)
        {
            if (__instance.dead)
            {
                __instance.rb.constraints = __instance.defaultRBConstraints;
                __instance.cc.enabled = true;
            }

        }
        // Adds the VR keybinds controller to the player and resets the turn offset.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.Start))]
        static void ScaleObjects(NewMovement __instance)
        {
            __instance.gameObject.AddComponent<VRPlayer.VRKeybindsController>();

            InputVars.TurnOffset = 0;
        }


        // Drives the FPS camera rotation from the headset and places the eye cameras on the FPS player, through portals if needed.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.LateUpdate))]
        static void HandleRotationsAndPositions(CameraController __instance)
        {
            // the platformer camera is handled by CrashBandicrootCCP; leave its eyes there
            if (__instance.platformerCamera) return;
            if (leftEye == null || rightEye == null) return;
            // back from 4-S or Clash mode: return the eyes to the FPS player
            if (__instance == CameraController.Instance && leftEye.transform.parent != __instance.transform.parent)
            {
                leftEye.transform.SetParent(__instance.transform.parent);
                rightEye.transform.SetParent(__instance.transform.parent);
            }

            if (!__instance.player && Vars.Registered<NewMovement>() is NewMovement NM) { __instance.nm = NM; __instance.player = NM.gameObject; }

            __instance.rotationX = -VRControllerLocations.Instance.headRot.eulerAngles.x;
            __instance.rotationY = VRControllerLocations.Instance.headRot.eulerAngles.y + InputVars.TurnOffset;
            __instance.tiltRotationZ = VRControllerLocations.Instance.headRot.eulerAngles.z;
            // After a portal or a gravity change the game rolls its camera back to level over 0.25-0.5 s (transitionRotationZ,
            // rotationOffset). On a flat screen that hides the cut; in VR it rolled the view against the head, a strong cue
            // for motion sickness. The eyes keep only the real head roll: the new orientation applies at once.
            __instance.transitionRotationZ = 0f; __instance.transitionRotationZSmooth = 0f;
            __instance.rotationOffset = Quaternion.identity;
            __instance.ApplyRotations();

            if (__instance == CameraController.Instance) UpdateHeightAdjust(__instance);
            Vector3 Push = WallPush();
            PortalAwareSetTransformFromBody(leftEye.transform, InputTracking.GetLocalPosition(XRNode.LeftEye), __instance.transform.rotation, false, Push);
            PortalAwareSetTransformFromBody(rightEye.transform, InputTracking.GetLocalPosition(XRNode.RightEye), __instance.transform.rotation, false, Push);
            EyesPlacedFrame = Time.frameCount; EyesThroughPortals = true;
        }

        // ---- Walls ----
        // The eyes follow the real head, which can lean or step past the player body's collider: walking up to a wall
        // then put the view inside it, which shows black. The view now stops a few centimetres before any level
        // geometry between the body and the head, the way a real head stops at a wall; leaning still moves the view
        // where there is room. One push for both eyes, so the stereo stays intact.
        private const float WallMargin = .12f; // metres kept between the eyes and a wall
        private static Vector3 WallPush()
        {
            CameraController cc = Vars.Registered<CameraController>();
            if (cc == null || cc.transform.parent == null) return Vector3.zero;
            Vector3 Head = InputTracking.GetLocalPosition(XRNode.Head);
            Quaternion ParentRot = cc.gravityRotation * Quaternion.AngleAxis(InputVars.TurnOffset, Vector3.up);
            // from the body's centre line at eye height out to the head, the way the eyes are placed below
            Vector3 From = cc.transform.parent.position + ParentRot * new Vector3(0f, Head.y + HeightAdjust, 0f);
            Vector3 Out = ParentRot * new Vector3(Head.x - HeadCentre.x, 0f, Head.z - HeadCentre.y);
            float Distance = Out.magnitude;
            if (Distance < .001f) return Vector3.zero;
            Vector3 Direction = Out / Distance;
            if (!Physics.SphereCast(From, WallMargin, Direction, out RaycastHit Hit, Distance, LayerMaskDefaults.Get(LMD.Environment), QueryTriggerInteraction.Ignore))
                return Vector3.zero;
            return Direction * Hit.distance - Out;
        }

        // ---- Height ----
        // The eyes were placed at the player body plus the raw tracked height, so the view height depended on the
        // player's real height, on sitting or standing, and on the tracking origin (10 issues: "constant crouch",
        // "too tall", "weapons huge when short", "can't jump past the tutorial"). With "Auto height" the tracked head
        // height is measured once per level (and on the "Recalibrate height" key) and mapped to the game's own eye
        // height, so everybody sees the levels from where the game expects; crouching and leaning stay real.
        // "Height offset (meters)" is added on top either way. The same adjustment moves the hands, so they stay
        // where they are relative to the head.
        private const int CalibrationDelayFrames = 45; // let tracking settle after a level loads
        private static float HeightAdjust;
        private static CameraController CalibratedFor;
        private static int FramesSinceCamera;
        private static bool CalibrationRequested;
        private static float GameEyeHeight, CalibratedHeadHeight = float.NaN;
        // The tracked head's x/z at the last recenter. The eyes were placed at the player body plus the head's offset
        // from the play area's centre, so a player standing away from that centre saw from beside or ahead of the body
        // (and into walls the body touched). At each recenter the head's spot becomes the body's spot; the hands move
        // with it, so they stay where they are relative to the head.
        private static Vector2 HeadCentre;

        // Asks for a new height calibration on the next frames (the "Recalibrate height" key).
        public static void RequestHeightCalibration() => CalibrationRequested = true;

        private static void UpdateHeightAdjust(CameraController cc)
        {
            if (cc != CalibratedFor)
            {
                // a new level: its camera's start height is the game's eye height (defaultPos moves down while sliding later)
                CalibratedFor = cc;
                FramesSinceCamera = 0;
                GameEyeHeight = cc.defaultPos.y;
                CalibratedHeadHeight = float.NaN;
            }
            FramesSinceCamera++;

            // recentering runs with auto height off too; only the height part depends on it
            if (CalibrationRequested || (float.IsNaN(CalibratedHeadHeight) && FramesSinceCamera >= CalibrationDelayFrames))
            {
                CalibrationRequested = false;
                Vector3 Head = InputTracking.GetLocalPosition(XRNode.Head);
                CalibratedHeadHeight = Head.y;
                HeadCentre = new Vector2(Head.x, Head.z);
                Vars.Log.LogInfo($"Height calibrated: head {CalibratedHeadHeight:0.00} m above the tracking floor, game eye height {GameEyeHeight:0.00} m"
                               + $" above the player body, \"Height offset\" {Vars.Config.HeightOffset:0.00} m"
                               + $"{(Vars.Config.AutoHeight ? "" : " (auto height off: not applied)")}; head centred on the body, {HeadCentre.magnitude:0.00} m from the play area centre.");
            }

            float Adjust = Vars.Config.HeightOffset;
            if (Vars.Config.AutoHeight && !float.IsNaN(CalibratedHeadHeight)) Adjust += GameEyeHeight - CalibratedHeadHeight;
            HeightAdjust = Adjust;
            LogTrackedHeightOnce(InputTracking.GetLocalPosition(XRNode.Head).y + HeightAdjust);
        }

        // Logs the eye height above the player body once per game, so a player can tune "Height offset" from a number.
        private static bool LoggedTrackedHeight;
        private static void LogTrackedHeightOnce(float eyeHeight)
        {
            if (LoggedTrackedHeight || FramesSinceCamera < CalibrationDelayFrames + 1) return;
            LoggedTrackedHeight = true;
            Vars.Log.LogInfo($"Eye height above the player body: {eyeHeight:0.00} m (auto height {(Vars.Config.AutoHeight ? "on" : "off")}, "
                           + $"\"Height offset\" {Vars.Config.HeightOffset:0.00} m). If the view is too low or too high, change \"Height offset\" in VRTRAKILL_Config.json.");
        }

        // The rotation from tracking space to the world: stick turning and the player's gravity (as the hands are placed).
        public static Quaternion TrackingToWorld()
        {
            CameraController cc = Vars.Registered<CameraController>();
            return cc == null ? Quaternion.identity : cc.gravityRotation * Quaternion.AngleAxis(InputVars.TurnOffset, Vector3.up);
        }

        // Places a transform at a pose relative to the player body, with turn and gravity applied, then moves it through crossed portals.
        public static void PortalAwareSetTransformFromBody(Transform t, Vector3 localPosition, Quaternion worldRotation, bool hands = false, Vector3 worldPush = default)
        {
            CameraController cc = Vars.Registered<CameraController>();
            if (cc == null || cc.transform.parent == null) return;
            Quaternion parentRot =
                cc.gravityRotation *
                Quaternion.AngleAxis(InputVars.TurnOffset, Vector3.up);

            // auto height and "Height offset" move the eyes and the hands together, along the player's up (see UpdateHeightAdjust)
            localPosition.y += HeightAdjust;
            // and the recenter puts the head, not the play area's centre, on the body (see HeadCentre)
            localPosition.x -= HeadCentre.x;
            localPosition.z -= HeadCentre.y;

            Vector3 transformedLocalPos = parentRot * localPosition;
            t.position = cc.transform.parent.position + transformedLocalPos + worldPush;

            if (hands) t.rotation = parentRot * worldRotation;
            else t.rotation = worldRotation;

            MoveFromPlayerThroughPortals(t);
        }

        // Moves a transform through any portal crossed between the player and the transform.
        public static void MoveFromPlayerThroughPortals(Transform t)
        {
            CameraController cc = Vars.Registered<CameraController>();
            PortalManagerV2 pm = Vars.Registered<PortalManagerV2>();
            if (cc == null || cc.transform.parent == null || pm == null || pm.Scene == null) return;
            PortalScene Scene = pm.Scene;

            PortalHandle portalHandle;
            Vector3 intersection;
            PortalTravellerFlags portalTravellerFlags = PortalTravellerFlags.Player;

            // adapted from PortalManagerV2
            if (Scene.FindCrossedPortal(cc.transform.parent.position, t.transform.position, out portalHandle, out intersection))
            {
                Portal portalObject = Scene.GetPortalObject(portalHandle);
                PortalTravellerFlags travelFlags = portalObject.GetTravelFlags(portalHandle.side);
                bool canTravel = travelFlags.HasFlag(portalTravellerFlags);
                PortalHandleSequence portalSequence = new PortalHandleSequence(new PortalHandle[]
                {
                    portalHandle
                });
                if (canTravel)
                {
                    Matrix4x4 travelMatrix = Scene.GetTravelMatrix(portalHandle);
                    Vector3 vector2 = travelMatrix.MultiplyPoint3x4(intersection);
                    Vector3 direction = travelMatrix.MultiplyPoint3x4(t.transform.position) - vector2;
                    PortalTraversalV2[] intersections;
                    PortalPhysicsV2.ProjectThroughPortals(vector2, direction, pm.empty_lm, out _, out _, out intersections);
                    for (int i = 0; i < intersections.Length; i++)
                    {
                        PortalHandle portalHandle2 = intersections[i].portalHandle;
                        if (!Scene.GetPortalObject(portalHandle2).GetTravelFlags(intersections[i].portalHandle.side).HasFlag(portalTravellerFlags))
                        {
                            canTravel = false;
                            break;
                        }
                    }
                    if (canTravel)
                    {
                        if (intersections.Length != 0)
                        {
                            portalSequence = PortalHandleSequence.Prepend(portalHandle, intersections);
                            travelMatrix = Scene.GetTravelMatrix(portalSequence);
                        }
                        PortalTravelDetails details = PortalTravelDetails.WithInteresction(portalSequence, intersections, travelMatrix, intersection);

                        t.transform.position = details.enterToExit.MultiplyPoint3x4(t.transform.position);
                        t.transform.rotation = details.enterToExit.rotation * t.transform.rotation;
                    }
                }
            }
        }

        // Stores the camera yaw before CameraController.Transform runs.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.Transform))]
        static void TransformBefore(CameraController __instance, ref float __state)
        {
            __state = __instance.rotationY;
        }
        // Adds the yaw change from CameraController.Transform to the turn offset.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.Transform))]
        static void TransformAfter(CameraController __instance, ref float __state)
        {
            InputVars.TurnOffset += __instance.rotationY - __state;
        }

        // Returns the gun hand position as the camera default position.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.GetDefaultPos))]
        static bool GetDefaultPos(ref Vector3 __result)
        {
            // Instance never returns null: without a VR hand it auto-creates a VRGunsSystem with no GunOffset
            Controllers.VRGunsSystem Guns = Controllers.VRGunsSystem.Instance;
            __result = Guns != null && Guns.GunOffset != null ? Guns.GunOffset.transform.position : Vector3.zero;
            return false;
        }

        // the player model material for a head whose eye camera did not exist yet
        static Material PendingHeadMaterial;
        // Adds the VR head, with the player model's material, to the left eye.
        static void AddHead(Material PlayerMaterial)
        {
            GameObject head = GameObject.Instantiate(Assets.VHead, leftEye.transform);
            head.transform.localPosition = new Vector3(0, 0, -0.07f);
            head.transform.localScale *= 2f;
            Object.Destroy(head.GetComponent<CapsuleCollider>());
            foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = LayerMask.NameToLayer("Portal");
            // reuse the player model's material for the correct unlit look
            head.GetComponentInChildren<SkinnedMeshRenderer>().material = PlayerMaterial;
        }

        // Adds the VR head to the left eye, or after the eyes exist, and removes the player model renderers.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerAnimations), nameof(PlayerAnimations.Start))]
        static void RemovePlayerModel(PlayerAnimations __instance)
        {
            // read before the renderers are destroyed below
            Material PlayerMaterial = __instance.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>().material;
            // leftEye can still be the previous scene's destroyed eye when this Start runs before PortalManagerV2.OnEnable
            if (leftEye != null) AddHead(PlayerMaterial);
            else PendingHeadMaterial = PlayerMaterial;

            __instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList().ForEach(x => UnityEngine.Object.Destroy(x));
            __instance.GetComponentsInChildren<MeshRenderer>(true).ToList().ForEach(x => UnityEngine.Object.Destroy(x));
            __instance.GetComponentsInChildren<GunColorGetter>(true).ToList().ForEach(x => UnityEngine.Object.Destroy(x));
        }
    }
}
