using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ULTRAKILL.Portal;
using ULTRAKILL.Portal.Native;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

namespace Plugin.Systems.VRCamera.Patches
{
    // Mirrors in the headset (issue #2, Quest through Virtual Desktop: mirrors broken in one eye or both). Two faults,
    // both from the game drawing portals for one centred camera and the mod drawing them twice, once per eye.
    //
    // 1. Off-centre eyes. The game draws what a mirror shows from a camera reflected in the mirror. A reflected camera is
    // a mirrored one, so the game turns it back into an ordinary camera and flips the picture left to right afterwards
    // (PortalRenderV2.defaultProjectionMatrixMirrored = mirrorMatrix * projection, and the shaders under _InMirroredSpace).
    // That flip gives the true reflection only when the projection is centred on the view direction: flip * P = P * flip
    // needs P[0,2] = 0. A monitor camera's projection is centred. A headset eye's is not: each eye sees further to the
    // outside than toward the nose, so P[0,2] is (right + left) / (right - left) of the eye's field of view (-0.17 for
    // tangents -1.39 / 0.99). The flipped picture then lands 2 * P[0,2] to the side in normalised screen units, the two
    // eyes in opposite directions, while what is culled is still worked out from the mirror's real outline; the masks of
    // portals seen in a mirror miss their picture too. SteamVR's null driver has a centred projection, so no test without
    // a headset could show it. Every view on the mirrored side now gets a shear in view space, x += k * z with
    // k = -2 * P[0,2] / P[0,0], under the game's flip: flip * P * shear * view = P * (the true reflected view). Views on
    // the normal side are not touched, and with a centred projection k is 0 and nothing changes.
    //
    // 2. A flag left on for the other eye. The depth prepass sets _InMirroredSpace for each portal it masks and leaves the
    // last value set; the game's Render puts it back to 0 after each portal it draws, so its own camera always drew the
    // level with 0. With two eyes the right eye's prepass runs after the left eye's, and the left eye draws before the
    // right eye's Render, so a left eye that drew no portal that frame (a mirror at the right edge of the view, seen by
    // the right eye only) drew the whole level with the right eye's last value, which can be 1: the shaders' mirror flip
    // on everything. Each eye's Render now starts from 0.
    //
    // "Mirror fix for VR eyes" in the config turns both off.
    [HarmonyPatch]
    internal static class MirrorStereoP
    {
        private static bool Enabled => Vars.Config.Misc.MirrorFix && XRSettings.enabled;
        private static readonly int InMirroredSpaceID = Shader.PropertyToID("_InMirroredSpace");

        // true once the transpiler below found where Render places the portal camera; without it the command buffers
        // would be sheared and the camera not, which is worse than no fix, so nothing is sheared
        private static bool CameraHooked;
        private static bool Broken;

        // The view-space shear that makes a renderer's mirrored views right for its eye's projection; 0 for a centred one.
        private static float ShearOf(PortalRenderV2 Renderer)
        {
            Matrix4x4 P = Renderer.defaultProjectionMatrix;
            if (!(Mathf.Abs(P.m00) > 1e-4f)) return 0f;
            float K = -2f * P.m02 / P.m00;
            return float.IsNaN(K) || float.IsInfinity(K) || Mathf.Abs(K) < 1e-5f ? 0f : K;
        }

        private static Matrix4x4 Shear(float K)
        {
            Matrix4x4 M = Matrix4x4.identity;
            M.m02 = K;
            return M;
        }

        // Shears the mirrored-side views the depth prepass and the portal draws use. Setup has just filled them for this
        // frame; Render reads the same lists when the eye draws.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PortalRenderV2), nameof(PortalRenderV2.DepthPrepass))]
        static void ShearMirroredViews(PortalRenderV2 __instance)
        {
            if (!Enabled || !CameraHooked || Broken || __instance.scene == null) return;
            float K = ShearOf(__instance);
            if (K == 0f) return;
            // in its own method, so even a failure to compile it (the game's private portal types) only turns the fix off
            try { ShearLists(__instance, K); }
            catch (System.Exception E)
            {
                Broken = true;
                Vars.Log.LogError("Mirror fix for VR eyes failed and is off for this game: " + E);
            }
        }

        private static void ShearLists(PortalRenderV2 Renderer, float K)
        {
            NativeList<NativePortal> Portals = Renderer.scene.nativeScene.renderPortals;
            NativeList<PortalRenderV2.PrepassData> Prepass = Renderer.prepassPortals;
            NativeList<PortalRenderV2.RenderData> Renders = Renderer.renderDatas;
            if (!Portals.IsCreated || !Prepass.IsCreated || !Renders.IsCreated) return;

            float4x4 KF = Shear(K);
            Matrix4x4 KM = Shear(K);
            bool Any = false;
            for (int i = 0; i < Prepass.Length; i++)
            {
                PortalRenderV2.PrepassData D = Prepass[i];
                // the -1 entry draws the parent portal from the parent view, which is on the other side of that portal
                bool Mirrored = D.handleIndex == -1 ? D.inMirroredSpace ^ IsMirror(Portals, D.parentHandleIndex) : D.inMirroredSpace;
                if (!Mirrored) continue;
                D.viewMatrix = math.mul(KF, D.viewMatrix);
                Prepass[i] = D;
                Any = true;
            }
            for (int i = 0; i < Renders.Length; i++)
            {
                PortalRenderV2.RenderData D = Renders[i];
                bool Changed = false;
                if (D.inMirroredSpace) { D.enterViewMatrix = KM * D.enterViewMatrix; Changed = true; }
                if (D.parentIndex != ushort.MaxValue && (D.inMirroredSpace ^ IsMirror(Portals, D.parentIndex)))
                { D.parentViewMatrix = KM * D.parentViewMatrix; Changed = true; }
                if (D.outMirroredSpace) Any = true;
                if (Changed) Renders[i] = D;
            }
            if (Any) LogOnce(Renderer, K);
        }

        private static bool IsMirror(NativeList<NativePortal> Portals, int Index)
            => Index >= 0 && Index < Portals.Length && Portals[Index].renderData.mirror;

        // Puts the portal camera's view through the same shear when it draws a mirrored view, right after Render places it.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(PortalRenderV2), nameof(PortalRenderV2.Render))]
        static IEnumerable<CodeInstruction> ShearPortalCamera(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            MethodInfo SetPose = AccessTools.Method(typeof(Transform), nameof(Transform.SetPositionAndRotation), new[] { typeof(Vector3), typeof(Quaternion) });
            FieldInfo ViewRot = AccessTools.Field(typeof(PortalRenderV2.RenderData), nameof(PortalRenderV2.RenderData.viewRot));
            List<CodeInstruction> Code = new List<CodeInstruction>(instructions);
            // portalCam.transform.SetPositionAndRotation(renderData.viewPos, renderData.viewRot): the load before viewRot is the renderData local
            for (int i = 2; i < Code.Count; i++)
            {
                if (!Code[i].Calls(SetPose) || !Code[i - 1].LoadsField(ViewRot)) continue;
                CodeInstruction Data = LoadByValue(Code[i - 2]);
                if (Data == null) break;
                Code.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Data,
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MirrorStereoP), nameof(PlacePortalCamera))),
                });
                CameraHooked = true;
                return Code;
            }
            Vars.NoMatch(original, "portalCam.transform.SetPositionAndRotation(renderData.viewPos, renderData.viewRot)")(null);
            return Code;
        }

        private static CodeInstruction LoadByValue(CodeInstruction Load)
        {
            if (Load.opcode == OpCodes.Ldloca_S) return new CodeInstruction(OpCodes.Ldloc_S, Load.operand);
            if (Load.opcode == OpCodes.Ldloca) return new CodeInstruction(OpCodes.Ldloc, Load.operand);
            if (Load.opcode == OpCodes.Ldloc_S || Load.opcode == OpCodes.Ldloc || Load.opcode == OpCodes.Ldloc_0
                || Load.opcode == OpCodes.Ldloc_1 || Load.opcode == OpCodes.Ldloc_2 || Load.opcode == OpCodes.Ldloc_3)
                return new CodeInstruction(Load.opcode, Load.operand);
            return null;
        }

        // the portal camera drawing a mirrored view right now and its shear, for the sky pictures it renders (SkyboxP)
        private static Camera ShearedCamera;
        private static float ShearedK;

        private static void PlacePortalCamera(PortalRenderV2 Renderer, PortalRenderV2.RenderData Data)
        {
            ShearedCamera = null;
            Camera C = Renderer.portalCam;
            if (C == null) return;
            // always: the game never sets this matrix itself, and a shear must not outlive the setting
            C.ResetWorldToCameraMatrix();
            if (!Data.outMirroredSpace || !Enabled || Broken) return;
            float K = ShearOf(Renderer);
            if (K == 0f) return;
            C.worldToCameraMatrix = Shear(K) * C.worldToCameraMatrix;
            ShearedCamera = C; ShearedK = K;
        }

        // Each eye's portals start with the mirror flag off (fault 2 above).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PortalRenderV2), nameof(PortalRenderV2.Render))]
        static void StartUnmirrored()
        {
            if (Enabled) Shader.SetGlobalFloat(InMirroredSpaceID, 0f);
        }

        // Gives the portal camera its own view back once the portals are drawn.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortalRenderV2), nameof(PortalRenderV2.Render))]
        static void ResetPortalCamera(PortalRenderV2 __instance)
        {
            if (__instance.portalCam != null) __instance.portalCam.ResetWorldToCameraMatrix();
            ShearedCamera = null;
        }

        // The projection a sky camera copying this camera needs: the shear goes with it when the camera draws a mirrored view.
        internal static Matrix4x4 SkyProjection(Camera Cam)
            => Cam != null && Cam == ShearedCamera ? Cam.projectionMatrix * Shear(ShearedK) : Cam.projectionMatrix;

        // once per eye and level, so a log shows the correction ran and by how much
        private static readonly HashSet<int> Logged = new HashSet<int>();
        private static void LogOnce(PortalRenderV2 Renderer, float K)
        {
            if (Logged.Count > 16 || !Logged.Add(Renderer.GetInstanceID())) return;
            bool Left = Vars.Registered<PortalManagerV2>() is PortalManagerV2 PM && PM.render == Renderer;
            Vars.Log.LogInfo($"Mirrors: the {(Left ? "left" : "right")} eye's projection is off-centre (P[0,2] {Renderer.defaultProjectionMatrix.m02:0.000}),"
                           + $" so its mirrored views are sheared by {K:0.000} to land where the reflection is (\"Mirror fix for VR eyes\").");
        }
    }
}
