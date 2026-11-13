using UnityEngine;
using UnityEngine.Rendering;

namespace Plugin.Systems.VRCamera
{
    // Comfort vignette: darkens the edge of the view while the player moves fast or turns with the stick, which
    // reduces motion sickness for players who need it. Off by default ("Comfort vignette"), because ULTRAKILL is about
    // speed and most players want the full view.
    // Like the tunnelling vignette of Unity's XR toolkit, it is a head-locked mesh just in front of the eyes, drawn over
    // the world (ZTest Always, overlay queue) on the hands' layer, which both eye cameras render; the UI camera draws
    // menus over it. A command buffer drawing a full-screen quad on the eye cameras did not reach the headset image.
    internal sealed class ComfortVignette : MonoBehaviour
    {
        private const float Distance = .25f; // metres in front of the eyes
        private static Material Mat;
        private static Mesh Quad;
        private static float LastZoom = -1f;
        private Camera Left, Right;
        private Transform Vignette;
        private MeshRenderer Renderer;
        internal static float Amount { get; private set; } // 0 none, 1 strongest
        // developer test hook only: forces the amount
        internal static float? TestAmount;

        // Adds the vignette to the eye pair once (on the left eye, so it goes away with the eyes).
        public static void Attach(Camera LeftEye, Camera RightEye)
        {
            if (LeftEye == null || RightEye == null || LeftEye.GetComponent<ComfortVignette>() != null) return;
            ComfortVignette V = LeftEye.gameObject.AddComponent<ComfortVignette>();
            V.Left = LeftEye; V.Right = RightEye;
        }

        private void Start()
        {
            if (Mat == null) Build();
            LastZoom = -1f;
            GameObject G = new GameObject("VRTRAKILL Comfort Vignette", typeof(MeshFilter), typeof(MeshRenderer));
            G.layer = (int)Layers.AlwaysOnTop;
            G.transform.SetParent(transform, false);
            G.GetComponent<MeshFilter>().sharedMesh = Quad;
            Renderer = G.GetComponent<MeshRenderer>();
            Renderer.sharedMaterial = Mat;
            Renderer.shadowCastingMode = ShadowCastingMode.Off; Renderer.receiveShadows = false;
            Renderer.enabled = false;
            Vignette = G.transform;
        }

        // A quad reaching 80 degrees from the centre of view at Distance, with a black texture whose alpha grows from
        // the clear centre to the edge (clamped, so everything past the ring stays dark), and a UI material drawn over
        // everything. The ring closes in by zooming the texture, not the quad, so the quad always covers the view.
        private static void Build()
        {
            const int Size = 128;
            Texture2D Tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VRTRAKILL vignette" };
            var Pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + .5f) / Size * 2f - 1f, dy = (y + .5f) / Size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .8f, d));
                    Pixels[y * Size + x] = new Color32(0, 0, 0, (byte)(a * 255));
                }
            Tex.SetPixels32(Pixels); Tex.Apply(false, true);
            Mat = new Material(Shader.Find("UI/Default")) { name = "VRTRAKILL vignette", mainTexture = Tex, renderQueue = 4000 };
            Mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            float H = Distance * Mathf.Tan(80f * Mathf.Deg2Rad);
            Quad = new Mesh { name = "VRTRAKILL vignette quad" };
            Quad.vertices = new[] { new Vector3(-H, -H, 0), new Vector3(H, -H, 0), new Vector3(H, H, 0), new Vector3(-H, H, 0) };
            Quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Quad.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            Quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            Quad.RecalculateBounds();
        }

        private void LateUpdate()
        {
            Amount = TestAmount ?? Target(Amount);
            float A = Amount * Vars.Config.Comfort.VignetteStrength;
            if (Renderer == null) return;
            Renderer.enabled = A > .01f;
            Mat.color = new Color(1f, 1f, 1f, A);
            // texture zoom: at 0.72 the clear circle reaches about 70 degrees (only the edge of sight darkens), at 4.26
            // about 25 degrees, dark from about 47
            // done with the quad's texture coordinates: the UI shader ignores the material's texture scale
            float K = Mathf.Lerp(.72f, 4.26f, Amount);
            if (Mathf.Abs(K - LastZoom) > .01f)
            {
                LastZoom = K;
                float Lo = .5f - .5f * K, Hi = .5f + .5f * K;
                Quad.uv = new[] { new Vector2(Lo, Lo), new Vector2(Hi, Lo), new Vector2(Hi, Hi), new Vector2(Lo, Hi) };
            }
            // a child of the left eye, so it follows the head whenever the eyes are placed: in front of the middle of the
            // eyes (half the eye distance to the right of the left eye)
            if (Vignette != null && Right != null)
            {
                float HalfIPD = Left.transform.InverseTransformPoint(Right.transform.position).x * .5f;
                Vignette.localPosition = new Vector3(HalfIPD, 0f, Distance);
                Vignette.localRotation = Quaternion.identity;
            }
        }


        // Eases toward the target: speed above a run, and stick turning.
        private static float Target(float Current)
        {
            float Want = 0f;
            if (Vars.Config.Comfort.Vignette)
            {
                NewMovement NM = Vars.Registered<NewMovement>();
                if (NM != null && NM.rb != null && !Vars.IsPaused)
                {
                    Vector3 V = NM.rb.velocity; V.y *= .5f;
                    Want = Mathf.InverseLerp(12f, 35f, V.magnitude);
                }
                if (Mathf.Abs(Input.InputVars.TurnVector.x) > Vars.Config.Controllers.Deadzone && !Vars.Config.Controllers.SnapTurn) Want = Mathf.Max(Want, .8f);
            }
            return Mathf.MoveTowards(Current, Want, Time.unscaledDeltaTime * (Want > Current ? 6f : 2f));
        }
    }
}
