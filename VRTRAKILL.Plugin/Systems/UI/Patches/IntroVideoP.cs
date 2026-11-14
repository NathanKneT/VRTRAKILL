using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Plugin.Systems.UI.Patches
{
    // The New Blood logo at game start is a video that the game draws over its camera's view, full screen. In VR that
    // covered each eye edge to edge, and "NEW BLOOD INTERACTIVE" ran past both sides of the view (PS VR2 test). The
    // video now plays into a texture shown on a floating panel at the size of the other menus, behind the intro's
    // own fade and warning images.
    [HarmonyPatch]
    internal static class IntroVideoP
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(IntroViolenceScreen), nameof(IntroViolenceScreen.Start))]
        static void PlayOnPanel(IntroViolenceScreen __instance)
        {
            VideoPlayer Video = __instance.vp;
            if (Video == null) return;
            VideoRenderMode Mode = Video.renderMode;
            Vars.Log.LogInfo($"Intro video: render mode {Mode}, camera {(Video.targetCamera != null ? Video.targetCamera.name : "none")}.");
            if (Mode != VideoRenderMode.CameraFarPlane && Mode != VideoRenderMode.CameraNearPlane) return;

            var Texture = new RenderTexture(1920, 1080, 0);
            Video.renderMode = VideoRenderMode.RenderTexture;
            Video.targetTexture = Texture;

            // a screen space canvas at the game's reference size; the mod turns it into a floating panel like the menus
            var Root = new GameObject("VR Intro Video");
            Root.layer = (int)Layers.UI;
            Canvas Panel = Root.AddComponent<Canvas>();
            Panel.renderMode = RenderMode.ScreenSpaceOverlay;
            Panel.sortingOrder = -100; // the intro's fade to black and warning screen draw over it
            CanvasScaler Scaler = Root.AddComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.referenceResolution = new Vector2(1920, 1080);
            Root.AddComponent<IntroVideoTexture>().Texture = Texture;

            var Picture = new GameObject("Video", typeof(RectTransform));
            Picture.layer = (int)Layers.UI;
            Picture.transform.SetParent(Root.transform, false);
            RectTransform Rect = (RectTransform)Picture.transform;
            Rect.anchorMin = Vector2.zero; Rect.anchorMax = Vector2.one;
            Rect.offsetMin = Vector2.zero; Rect.offsetMax = Vector2.zero;
            Picture.AddComponent<RawImage>().texture = Texture;

            // converted now if the VR UI cameras exist; otherwise the late canvas conversion after a scene change does it
            if (UIConverter.UICamera != null) UIConverter.ConvertCanvas(Panel);
        }
    }

    // Frees the intro video's texture with the panel when the intro scene unloads.
    internal sealed class IntroVideoTexture : MonoBehaviour
    {
        public RenderTexture Texture;

        private void OnDestroy()
        {
            if (Texture == null) return;
            Texture.Release();
            Destroy(Texture);
        }
    }
}
