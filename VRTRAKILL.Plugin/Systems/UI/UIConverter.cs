using UnityEngine;
using UnityEngine.UI;
using VRTRAKILL.Utilities;

namespace Plugin.Systems.UI
{
    // mostly adapted from HuskVR
    internal class UIConverter
    {
        public static Camera UICamera { get; private set; }
        public static Camera UIEventCamera { get; private set; }

        // Creates the UI and UI event cameras and converts every canvas to world space.
        public static void ConvertAllCanvases()
        {
            if (Vars.MainCamera == null) return;
            UICamera = new GameObject("UI Camera").AddComponent<Camera>();
            UICamera.cullingMask = 1 << (int)Layers.UI;
            UICamera.clearFlags = CameraClearFlags.Depth; UICamera.depth = 1;
            // the default 0.3 m near plane clipped UI held close, like the wrist HUD read as a watch (~0.3 m from the eyes)
            UICamera.nearClipPlane = .02f;
            UICamera.transform.parent = Vars.MainCamera.transform;
            UICamera.transform.localPosition = Vector3.zero;
            UICamera.transform.rotation = Vars.MainCamera.transform.rotation;
            // The menus and the floating HUD sit 72 m away. With occlusion culling on, this camera skipped them whenever
            // a level's walls stood in between, which in a level is nearly always: the pause menu never showed, and the
            // floating HUD came and went (measured in Level 1-1: a UI square showed at 30 m but not at 50 m with a wall
            // at 41.5 m; the pause menu shows once culling is off). UI must never be culled by the level.
            UICamera.useOcclusionCulling = false;

            if (Vars.Config.UIInteraction.ControllerBased)
            {
                // disabled camera; only its viewport centre is used as the UI ray
                UIEventCamera = new GameObject("UI Event Camera").AddComponent<Camera>();
                UIEventCamera.enabled = false;
                UIEventCamera.stereoTargetEye = StereoTargetEyeMask.None;
                UIEventCamera.clearFlags = CameraClearFlags.Nothing;
                UIEventCamera.depth = 99;
                UIEventCamera.fieldOfView = 1;
                UIEventCamera.nearClipPlane = .01f;
                UIEventCamera.cullingMask = -1;
            }
            else UIEventCamera = UICamera;
            UIEventCamera.gameObject.AddComponent<UIInteraction>();

            RecursiveConvertCanvas();
            foreach (Canvas C in Resources.FindObjectsOfTypeAll(typeof(Canvas)))
                if (!C.gameObject.HasComponent<UICanvas>())
                    RecursiveConvertCanvas(C.gameObject);
        }

        // Converts the canvas on an object and its children, or every scene canvas when no object is given.
        public static void RecursiveConvertCanvas(GameObject GO = null)
        {
            if (GO != null)
            {
                try { ConvertCanvas(GO.GetComponent<Canvas>()); } catch { }

                if (GO.transform.childCount > 0)
                    for (int i = 0; i < GO.transform.childCount; i++)
                        RecursiveConvertCanvas(GO.transform.GetChild(i).gameObject);
            }
            else
            {
                foreach (Canvas C in Object.FindObjectsOfType<Canvas>())
                    if (!C.gameObject.HasComponent<UICanvas>())
                        try { ConvertCanvas(C); } catch { }
            }
        }
        // Sets the UI event camera on a canvas and turns a screen space overlay canvas into a world space VR canvas.
        public static void ConvertCanvas(Canvas C, bool Force = false, bool DontAddComponent = false)
        {
            if (C == null) return; // callers pass GetComponent results; a missing canvas must not throw inside a game method
            if (C.GetComponent<VRCamera.MonitorOnly>() != null) return; // the streamer view's monitor canvas (StreamerOverlay)
            C.worldCamera = UIEventCamera;
            if (!Force && C.renderMode != RenderMode.ScreenSpaceOverlay) return;
            C.gameObject.layer = (int)Layers.UI;
            C.renderMode = RenderMode.WorldSpace;
            if (!DontAddComponent) C.gameObject.AddComponent<UICanvas>();

            foreach (Transform Child in C.transform) ConvertElement(Child);
        }
        // Makes the element and its children highlight red when hovered or selected.
        private static void ConvertElement(Transform Element)
        {
            if (Element.GetComponent<Selectable>() is Selectable Button)
            {
                ColorBlock block = Button.colors;
                block.highlightedColor = Color.red;
                block.selectedColor = Color.red; // stick selection must be as visible as laser hover
                Button.colors = block;
            }

            foreach (Transform Child in Element) ConvertElement(Child);
        }
    }
}
