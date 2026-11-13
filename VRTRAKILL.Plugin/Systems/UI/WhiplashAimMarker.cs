using UnityEngine;

namespace Plugin.Systems.UI
{
    // A small blue diamond ahead of the arm hand that shows where the whiplash will go. The whiplash is thrown along the
    // arm hand's aim (PatchHookArm), but nothing showed that aim; players asked for an off-hand crosshair (issues #36,
    // #77, #139). Shown only while the whiplash is ready to throw; "Whiplash aim marker" in the CBS settings turns it off.
    internal sealed class WhiplashAimMarker : MonoBehaviour
    {
        private static readonly Color MarkerColor = new Color(.35f, .8f, 1f, .85f);
        private HookArm Hook;
        private LineRenderer LR;
        private readonly Vector3[] Points = new Vector3[4];

        // Adds the marker under the whiplash, so it goes away with it.
        public static void Attach(HookArm hook)
        {
            if (!Vars.Config.CBS.WhiplashMarker || hook == null) return;
            GameObject go = new GameObject("VRTRAKILL Whiplash Aim Marker") { layer = (int)Layers.IgnoreRaycast };
            go.transform.SetParent(hook.transform, false);
            go.AddComponent<WhiplashAimMarker>().Hook = hook;
        }

        private void Awake()
        {
            // same renderer setup as the controller lasers (VRControllersSystem), which are known to draw in the headset
            LR = gameObject.AddComponent<LineRenderer>();
            LR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            LR.receiveShadows = false;
            LR.allowOcclusionWhenDynamic = false;
            LR.useWorldSpace = true;
            LR.loop = true;
            LR.positionCount = Points.Length;
            LR.sortingLayerName = "AlwaysOnTop";
            LR.material = new Material(Shader.Find("GUI/Text Shader"));
            LR.startColor = LR.endColor = MarkerColor;
            LR.enabled = false;
        }

        private void LateUpdate()
        {
            // Registered, not Vars.NonDominantHand: that one auto-creates a hand-less arms system when none exists yet
            Controllers.VRArmsSystem Arms = Visible() ? Vars.Registered<Controllers.VRArmsSystem>() : null;
            Transform Aim = Arms != null && Arms.GunOffset != null ? Arms.GunOffset.transform : null;
            if (Aim == null) { LR.enabled = false; return; }

            float Distance = Vars.Config.CBS.CrosshairDistance;
            Vector3 Center = Aim.position + Aim.forward * Distance;
            float Radius = .012f * Distance; // about 0.7 degrees, whatever the distance
            Vector3 Up = Aim.up * Radius, Right = Aim.right * Radius;
            Points[0] = Center + Up; Points[1] = Center + Right; Points[2] = Center - Up; Points[3] = Center - Right;
            LR.SetPositions(Points);
            LR.startWidth = LR.endWidth = Radius * .25f;
            LR.enabled = true;
        }

        // True while the whiplash can be thrown: equipped, idle, fists active, and no menu, shop or pause.
        private bool Visible()
        {
            if (Hook == null || !Hook.equipped || Hook.state != HookState.Ready) return false;
            if (Vars.IsMainMenu || Vars.IsPaused || Vars.IsPlayerFrozen || Vars.IsPlayerUsingShop) return false;
            FistControl Fists = Vars.Registered<FistControl>();
            return Fists != null && Fists.activated && !Fists.shopping;
        }
    }
}
