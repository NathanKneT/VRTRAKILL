using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.UI
{
    // Puts the health and stamina panel (the game's "GunCanvas": health, hard damage, stamina, weapon icon and charge)
    // on the back of the arm-hand forearm, turned toward the eyes, like a watch. Before, it floated at a fixed spot low
    // in the view. Looking at your arm to check your health is a VR habit (Half-Life: Alyx), and it frees the view.
    // "HUD on wrist" in the UI interaction settings turns it off. HUDPos still runs; this overrides its placement
    // every frame, after the hands have been placed. The UI camera's near plane is 2 cm (UIConverter) so a panel held
    // close is not clipped.
    internal sealed class WristHud : MonoBehaviour
    {
        private const float PanelWidth = 0.10f; // meters, width of what the panel shows (watch-sized)
        // in the arm controller's space: above the forearm, behind the hand
        private static readonly Vector3 WristOffset = new Vector3(0f, .05f, -.15f);
        private const int MeasureEvery = 60; // frames; parts of the panel come and go (railcannon charge, ...)

        private Vector3 ContentCenter; // centre of the shown graphics, in the canvas's own units
        private float ContentWidth;
        private int FramesToMeasure;
        private readonly Vector3[] Corners = new Vector3[4];

        // Always attached, so the setting can be switched in the VR settings column while playing.
        public static void Attach(Component Hud)
        {
            if (Hud == null || Hud.GetComponent<WristHud>() != null) return;
            Hud.gameObject.AddComponent<WristHud>();
        }

        private bool WasOn;

        private void LateUpdate()
        {
            if (!Vars.Config.UIInteraction.WristHud)
            {
                // switched off: the game's HUDPos puts the panel back where it floats (only on CheckPos, not every frame)
                if (WasOn) { WasOn = false; FramesToMeasure = 0; GetComponent<HUDPos>()?.CheckPos(); }
                return;
            }
            WasOn = true;
            // Registered: never auto-create a hand-less arms system
            Controllers.VRArmsSystem Arms = Vars.Registered<Controllers.VRArmsSystem>();
            if (Arms == null) return;
            Vector3 Head = HeadPosition();
            if (Head == Vector3.zero) return;
            if (--FramesToMeasure <= 0) { Measure(); FramesToMeasure = MeasureEvery; }
            if (ContentWidth <= 1f) return;

            Transform Hand = Arms.transform;
            Vector3 Pos = Hand.TransformPoint(WristOffset);
            CameraController CC = Vars.Registered<CameraController>();
            Vector3 Up = CC != null ? CC.gravityRotation * Vector3.up : Vector3.up;
            // a world-space canvas reads from the side its forward points away from
            transform.rotation = Quaternion.LookRotation(Pos - Head, Up);

            float Scale = PanelWidth / ContentWidth;
            Vector3 Parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            if (Parent.x == 0f || Parent.y == 0f || Parent.z == 0f) return;
            transform.localScale = new Vector3(Scale / Parent.x, Scale / Parent.y, Scale / Parent.z);
            // put the middle of what is shown at the wrist point, not the canvas's pivot
            transform.position = Pos - transform.TransformVector(ContentCenter);
        }

        // Finds the bounds of the visible graphics in the canvas's local units.
        private void Measure()
        {
            Vector3 Min = new Vector3(float.MaxValue, float.MaxValue, 0), Max = new Vector3(float.MinValue, float.MinValue, 0);
            bool Any = false;
            foreach (Graphic G in GetComponentsInChildren<Graphic>(false))
            {
                if (!G.enabled || G.color.a <= 0f) continue;
                G.rectTransform.GetWorldCorners(Corners);
                foreach (Vector3 C in Corners)
                {
                    Vector3 L = transform.InverseTransformPoint(C);
                    Min = Vector3.Min(Min, L); Max = Vector3.Max(Max, L);
                }
                Any = true;
            }
            if (!Any) return;
            ContentCenter = new Vector3((Min.x + Max.x) * .5f, (Min.y + Max.y) * .5f, 0);
            ContentWidth = Max.x - Min.x;
        }

        // The middle of the two VR eyes; zero before they exist.
        private static Vector3 HeadPosition()
        {
            Camera L = VRCamera.Patches.CameraConverterP.leftEye, R = VRCamera.Patches.CameraConverterP.rightEye;
            if (L == null || R == null) return Vector3.zero;
            return (L.transform.position + R.transform.position) * .5f;
        }
    }
}
