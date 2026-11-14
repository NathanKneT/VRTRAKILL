using System.Collections.Generic;
using UnityEngine;

namespace Plugin.Systems.VRCamera
{
    // What the streamer view draws over its picture, on the monitor only (IMGUI never reaches the headset):
    //  - a HUD, since health and stamina live on your wrist and the style meter at the edge of your view: health,
    //    stamina and the style rank with its meter ("HUD": Off, Small, Medium, Large);
    //  - with "Overlay" on Always: where your eyes look (PS VR2 eye tracking through the PSVR2 Toolkit), as a small
    //    hollow ring with a short trail, smoothed while the eyes rest and jumping with them on a saccade, fading during a
    //    blink; and what the triggers feel like right now (R2/L2: CLICK, CHARGE 2/4, READY, LOCKED...), since viewers
    //    cannot feel them;
    //  - with Always or Parries: an eye-aimed parry pulses the ring and draws its path, labelled "EYE-AIMED PARRY", and
    //    the parry warning tick flashes "PARRY". On Parries the gaze ring shows only around a parry.
    // The gaze is projected from the world (where the eyes' ray meets a wall or an enemy), so it stays right under the
    // view's smoothing.
    internal static class StreamerOverlay
    {
        private static Texture2D RingTex, DotTex;
        private static GUIStyle Label, Small;
        private static Vector2 Gaze; private static bool HaveGaze; private static float GazeAlpha;
        private static readonly Queue<(Vector2 P, float T)> Trail = new Queue<(Vector2, float)>();
        private const float TrailSeconds = .2f;

        // set by PatchPunch when a parry is aimed, and by ParryWarning when it ticks
        public static float LastParryTime = -10f, LastWarningTime = -10f;
        public static Vector3 LastParryFrom, LastParryTo;
        public static bool LastParryByEyes;

        public static bool WantsGaze => Vars.Config.StreamerView.On && Vars.Config.StreamerView.Overlay != "Off";

        public static void Draw(Rect View)
        {
            Camera Cam = StreamerView.WorldCamera;
            if (Cam == null) return;
            Setup(View);
            float Scale = HudScale();
            if (Scale > 0f) DrawHud(View, Scale);

            string Mode = Vars.Config.StreamerView.Overlay;
            if (Mode == "Off") return;
            float Now = Time.unscaledTime;
            bool NearParry = Now - LastParryTime < 1f;
            DrawGaze(View, Cam, Mode == "Always" || NearParry);
            if (Now - LastParryTime < .8f) DrawParry(View, Cam, Now - LastParryTime);
            if (Now - LastWarningTime < .3f) Text(new Rect(View.x, View.y + View.height * .62f, View.width, Label.fontSize * 1.6f), "PARRY", new Color(1f, .85f, .2f, 1f - (Now - LastWarningTime) / .3f), Label);
            if (Mode == "Always") DrawTriggers(View);
        }

        private static void Setup(Rect View)
        {
            if (RingTex == null) { RingTex = Ring(64, 25f, 31f); DotTex = Ring(16, 0f, 7f); }
            if (Label == null)
            {
                Label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
                Small = new GUIStyle(Label) { alignment = TextAnchor.MiddleLeft };
            }
            Label.fontSize = Mathf.RoundToInt(View.height / 30f);
            Small.fontSize = Mathf.RoundToInt(View.height / 45f);
        }

        // ---- Gaze ----
        private static void DrawGaze(Rect View, Camera Cam, bool Show)
        {
            float Dt = Time.unscaledDeltaTime;
            Camera L = Patches.CameraConverterP.leftEye, R = Patches.CameraConverterP.rightEye;
            Vector3 Dir = Vector3.forward;
            bool Fresh = L != null && R != null && Feedback.Psvr2Toolkit.TryGetGaze(.15f, out Dir);
            if (Fresh)
            {
                Vector3 From = (L.transform.position + R.transform.position) * .5f;
                Vector3 World = L.transform.rotation * Dir;
                Vector3 Point = Physics.Raycast(From, World, out RaycastHit Hit, 300f, LayerMaskDefaults.Get(LMD.EnemiesAndEnvironment), QueryTriggerInteraction.Ignore)
                    ? Hit.point : From + World * 50f;
                Vector3 V = Cam.WorldToViewportPoint(Point);
                if (V.z > 0f)
                {
                    Vector2 P = new Vector2(View.x + V.x * View.width, View.y + (1f - V.y) * View.height);
                    // jump with a saccade, rest smoothly during a fixation
                    if (!HaveGaze || (P - Gaze).magnitude > View.width * .04f) Gaze = P;
                    else Gaze = Vector2.Lerp(Gaze, P, 1f - Mathf.Pow(.5f, Dt / .04f));
                    HaveGaze = true;
                }
                else Fresh = false;
            }
            GazeAlpha = Mathf.MoveTowards(GazeAlpha, Fresh && Show ? 1f : 0f, Dt / .2f);
            float Now = Time.unscaledTime;
            if (HaveGaze && Fresh) Trail.Enqueue((Gaze, Now));
            while (Trail.Count > 0 && Now - Trail.Peek().T > TrailSeconds) Trail.Dequeue();
            if (!HaveGaze || GazeAlpha <= 0f) return;

            float Size = View.height / 60f; // about 18 px at 1080
            foreach (var (P, T) in Trail)
            {
                float A = (1f - (Now - T) / TrailSeconds) * .25f * GazeAlpha;
                Tex(DotTex, P, Size * .35f, new Color(1f, 1f, 1f, A));
            }
            float Pulse = Now - LastParryTime < .5f && LastParryByEyes ? 1f + (1f - (Now - LastParryTime) / .5f) * 1.2f : 1f;
            Tex(RingTex, Gaze, Size * Pulse, new Color(1f, 1f, 1f, .5f * GazeAlpha));
        }

        // ---- Parry: the path from the arm hand to where it was sent ----
        private static void DrawParry(Rect View, Camera Cam, float Age)
        {
            float A = 1f - Age / .8f;
            Vector3 F = Cam.WorldToViewportPoint(LastParryFrom), T = Cam.WorldToViewportPoint(LastParryTo);
            if (F.z > 0f && T.z > 0f)
            {
                Vector2 P0 = new Vector2(View.x + F.x * View.width, View.y + (1f - F.y) * View.height);
                Vector2 P1 = new Vector2(View.x + T.x * View.width, View.y + (1f - T.y) * View.height);
                int Dots = 24;
                for (int i = 0; i <= Dots; i++)
                    Tex(DotTex, Vector2.Lerp(P0, P1, i / (float)Dots), View.height / 150f, new Color(1f, .55f, .1f, A * .9f));
            }
            if (LastParryByEyes)
                Text(new Rect(View.x, View.y + View.height * .12f, View.width, Label.fontSize * 1.6f), "EYE-AIMED PARRY", new Color(1f, .55f, .1f, A), Label);
        }

        // ---- What the triggers feel like ----
        private static void DrawTriggers(Rect View)
        {
            string Gun = Feedback.AdaptiveTriggers.Describe(Gun: true), Arm = Feedback.AdaptiveTriggers.Describe(Gun: false);
            if (Gun == null && Arm == null) return;
            bool Left = Vars.Config.Controllers.LeftHanded;
            float H = Small.fontSize * 1.5f, W = Small.fontSize * 9f;
            float X = View.xMax - W - Small.fontSize, Y = View.yMax - H * 2.4f;
            if (Gun != null) Text(new Rect(X, Y, W, H), (Left ? "L2  " : "R2  ") + Gun, new Color(1f, 1f, 1f, .85f), Small);
            if (Arm != null) Text(new Rect(X, Y + H, W, H), (Left ? "R2  " : "L2  ") + Arm, new Color(1f, 1f, 1f, .85f), Small);
        }

        // ---- HUD ----
        private static float HudScale()
        {
            switch (Vars.Config.StreamerView.Hud)
            {
                case "Off": return 0f;
                case "Small": return .75f;
                case "Large": return 1.35f;
                default: return 1f;
            }
        }

        private static void DrawHud(Rect View, float Scale)
        {
            NewMovement NM = Vars.Registered<NewMovement>();
            if (NM == null || Vars.IsMainMenu) return;
            float U = View.height / 54f * Scale; // a unit: 20 px at 1080
            float X = View.x + U * 1.5f, Y = View.yMax - U * 4.5f;
            // health: red bar, number on it
            Bar(new Rect(X, Y, U * 14f, U * 1.4f), Mathf.Clamp01(NM.hp / 100f), new Color(.85f, .1f, .1f, .9f));
            Text(new Rect(X, Y, U * 14f, U * 1.4f), NM.hp.ToString(), Color.white, Small);
            // stamina: three blue segments
            for (int i = 0; i < 3; i++)
                Bar(new Rect(X + i * U * 4.8f, Y + U * 1.8f, U * 4.4f, U * .6f), Mathf.Clamp01((NM.boostCharge - i * 100f) / 100f), new Color(.2f, .6f, 1f, .9f));

            StyleHUD Style = Vars.Registered<StyleHUD>();
            if (Style == null || Style.ranks == null || Style.ranks.Count == 0 || !(Style.comboActive || Style.forceMeterOn)) return;
            StyleRank Rank = Style.currentRank;
            float RW = U * 12f, RH = RW * .3f, RX = View.xMax - RW - U * 1.5f, RY = View.y + U * 1.5f;
            if (Rank.sprite != null)
            {
                Texture T = Rank.sprite.texture; Rect S = Rank.sprite.textureRect;
                float Aspect = S.height > 0 ? S.width / S.height : 3f;
                RH = RW / Aspect;
                GUI.DrawTextureWithTexCoords(new Rect(RX, RY, RW, RH), T, new Rect(S.x / T.width, S.y / T.height, S.width / T.width, S.height / T.height));
            }
            Bar(new Rect(RX, RY + RH + U * .3f, RW, U * .4f), Rank.maxMeter > 0 ? Mathf.Clamp01(Style.currentMeter / Rank.maxMeter) : 0f, new Color(1f, 1f, 1f, .85f));
        }

        // ---- Drawing helpers ----
        private static void Bar(Rect R, float Fill, Color C)
        {
            Color Was = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .55f); GUI.DrawTexture(R, Texture2D.whiteTexture);
            GUI.color = C; GUI.DrawTexture(new Rect(R.x, R.y, R.width * Fill, R.height), Texture2D.whiteTexture);
            GUI.color = Was;
        }

        private static void Text(Rect R, string S, Color C, GUIStyle Style)
        {
            Color Was = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, C.a * .8f);
            GUI.Label(new Rect(R.x + 2f, R.y + 2f, R.width, R.height), S, Style);
            GUI.color = C;
            GUI.Label(R, S, Style);
            GUI.color = Was;
        }

        private static void Tex(Texture T, Vector2 Centre, float Radius, Color C)
        {
            Color Was = GUI.color; GUI.color = C;
            GUI.DrawTexture(new Rect(Centre.x - Radius, Centre.y - Radius, Radius * 2f, Radius * 2f), T);
            GUI.color = Was;
        }

        // a white ring (or disc, from 0) with a dark edge on both sides, so it shows on bright and dark scenes
        private static Texture2D Ring(int Size, float Inner, float Outer)
        {
            var T = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            float C = (Size - 1) * .5f, S = Size / 64f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float D = new Vector2(x - C, y - C).magnitude;
                    float In = Inner * S, Out = Outer * S, Edge = 1.5f * S;
                    Color P = Color.clear;
                    if (D >= In && D <= Out) P = (D < In + Edge && Inner > 0f) || D > Out - Edge ? new Color(0f, 0f, 0f, .8f) : Color.white;
                    T.SetPixel(x, y, P);
                }
            T.Apply();
            return T;
        }
    }
}
