﻿using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.VRCamera
{
    // Marks a canvas the VR conversion must leave alone (UIConverter.ConvertCanvas): it is for the monitor only.
    internal sealed class MonitorOnly : MonoBehaviour { }

    // The monitor side of the streamer view: a screen space overlay canvas, which Unity draws on the game window and
    // never in the headset. (An IMGUI version drew into the right eye's picture while VR ran: the PS VR2 tester saw the
    // streamer view in the top of the right lens.) It shows the streamer picture letterboxed to 16:9 and, on top:
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
    [DefaultExecutionOrder(1001)] // after StreamerView has drawn the picture
    internal sealed class StreamerOverlay : MonoBehaviour
    {
        // set by PatchPunch when a parry is aimed, and by ParryWarning when it ticks
        public static float LastParryTime = -10f, LastWarningTime = -10f;
        public static Vector3 LastParryFrom, LastParryTo;
        public static bool LastParryByEyes;
        // for the developer test hook: how many frames the monitor showed the picture
        internal static int Shown;

        public static bool WantsGaze => Vars.Config.StreamerView.On && Vars.Config.StreamerView.Overlay != "Off";

        private static StreamerOverlay Instance;
        private const int TrailDots = 12, PathDots = 25;
        private const float TrailSeconds = .2f;

        private Canvas Canvas;
        private RawImage Picture;
        private RectTransform View, Hud, Rank, RankFill, HealthFill, Ring;
        private readonly RectTransform[] Stamina = new RectTransform[3];
        private Image RingImage, RankImage;
        private readonly List<Image> Trail = new List<Image>(), Path = new List<Image>();
        private TMP_Text HealthText, ParryText, WarningText, GunText, ArmText;
        private bool FontSet, Logged;
        private Vector2 Gaze; private bool HaveGaze; private float GazeAlpha;
        private readonly Queue<(Vector2 P, float T)> TrailPoints = new Queue<(Vector2, float)>();

        // A child of the mod's hidden host object: the game destroys visible objects kept across scenes shortly after it
        // starts (see ModHost), and took this canvas with them, so the monitor never showed the picture.
        public static void Create(GameObject Host)
        {
            if (Instance != null) return;
            GameObject G = new GameObject("VRTRAKILL Streamer Screen", typeof(RectTransform));
            G.transform.SetParent(Host.transform, false);
            G.AddComponent<MonitorOnly>(); // before the canvas exists, so no conversion pass can take it
            Instance = G.AddComponent<StreamerOverlay>();
        }

        private void Awake()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 32000;
            CanvasScaler S = gameObject.AddComponent<CanvasScaler>();
            S.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            S.referenceResolution = new Vector2(1920, 1080);
            S.matchWidthOrHeight = 1f;
            Canvas.enabled = false;

            Box(transform, "Background", Vector2.zero, Vector2.one, Color.black);
            Picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            Picture.transform.SetParent(transform, false);
            Picture.raycastTarget = false;
            View = Picture.rectTransform;
            View.anchorMin = Vector2.zero; View.anchorMax = Vector2.one; View.sizeDelta = Vector2.zero;
            AspectRatioFitter Fit = Picture.gameObject.AddComponent<AspectRatioFitter>();
            Fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; Fit.aspectRatio = 16f / 9f;

            // HUD, bottom left: health bar with its number, three stamina segments above it
            Hud = Rect(View, "HUD", new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(280, 70));
            RectTransform HealthBack = Box(Hud, "Health", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f), new Vector2(0, 0), new Vector2(280, 30));
            HealthFill = Box(HealthBack, "Fill", Vector2.zero, new Vector2(1, 1), new Color(.85f, .1f, .1f, .9f));
            HealthText = Text(HealthBack, "Number", 22, TextAlignmentOptions.Center);
            for (int i = 0; i < 3; i++)
            {
                RectTransform Back = Box(Hud, "Stamina " + i, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f), new Vector2(i * 96, 38), new Vector2(88, 12));
                Stamina[i] = Box(Back, "Fill", Vector2.zero, new Vector2(1, 1), new Color(.2f, .6f, 1f, .9f));
            }
            // style rank, top right
            Rank = Rect(View, "Rank", Vector2.one, Vector2.one, new Vector2(-30, -30), new Vector2(260, 100));
            RankImage = new GameObject("Sprite", typeof(RectTransform)).AddComponent<Image>();
            RankImage.transform.SetParent(Rank, false);
            RankImage.preserveAspect = true; RankImage.raycastTarget = false;
            RectTransform RI = RankImage.rectTransform; RI.anchorMin = new Vector2(0, .25f); RI.anchorMax = Vector2.one; RI.sizeDelta = Vector2.zero;
            RectTransform MeterBack = Box(Rank, "Meter", new Vector2(0, 0), new Vector2(1, 0), new Color(0, 0, 0, .55f), Vector2.zero, new Vector2(0, 10));
            RankFill = Box(MeterBack, "Fill", Vector2.zero, new Vector2(1, 1), new Color(1, 1, 1, .85f));

            // gaze ring and its trail, the parry path, the labels
            Sprite RingSprite = Sprite.Create(RingTexture(64, 25f, 31f), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            Sprite Dot = Sprite.Create(RingTexture(16, 0f, 7f), new Rect(0, 0, 16, 16), new Vector2(.5f, .5f));
            for (int i = 0; i < TrailDots; i++) Trail.Add(Marker(Dot, 7f));
            for (int i = 0; i < PathDots; i++) Path.Add(Marker(Dot, 7f));
            RingImage = Marker(RingSprite, 36f); Ring = RingImage.rectTransform;
            ParryText = Text(Rect(View, "Parry", new Vector2(.5f, .88f), new Vector2(.5f, .88f), Vector2.zero, new Vector2(900, 60)), "Text", 40, TextAlignmentOptions.Center);
            WarningText = Text(Rect(View, "Warning", new Vector2(.5f, .38f), new Vector2(.5f, .38f), Vector2.zero, new Vector2(600, 50)), "Text", 34, TextAlignmentOptions.Center);
            RectTransform Triggers = Rect(View, "Triggers", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(320, 64));
            GunText = Text(Rect(Triggers, "Gun", new Vector2(0, .5f), new Vector2(1, 1), Vector2.zero, Vector2.zero), "Text", 24, TextAlignmentOptions.Right);
            ArmText = Text(Rect(Triggers, "Arm", new Vector2(0, 0), new Vector2(1, .5f), Vector2.zero, Vector2.zero), "Text", 24, TextAlignmentOptions.Right);
        }

        private void LateUpdate()
        {
            bool Show = StreamerView.Showing;
            if (Canvas.enabled != Show) Canvas.enabled = Show;
            SetMirror(!Show);
            if (!Show) return;
            Picture.texture = StreamerView.Picture;
            Shown++;
            // the game's font is loaded with its UI, after this canvas is made
            if (!FontSet && GameFont() is TMP_FontAsset Font)
            {
                FontSet = true;
                foreach (TMP_Text T in GetComponentsInChildren<TMP_Text>(true)) T.font = Font;
            }
            Camera Cam = StreamerView.WorldCamera;
            if (Cam == null) return;

            UpdateHud();
            if (!Logged && Shown > 30)
            {
                // once, after a few frames: whether the monitor canvas is what it should be, for a test log
                Logged = true;
                Vars.Log.LogInfo($"Streamer screen: canvas {Canvas.renderMode}, enabled {Canvas.isActiveAndEnabled}, converted to VR {GetComponent<UI.UICanvas>() != null}, "
                    + $"picture {View.rect.width:0}x{View.rect.height:0} of {Screen.width}x{Screen.height}, texture {(Picture.texture != null ? Picture.texture.width + "x" + Picture.texture.height : "none")}, HUD shown {Hud.gameObject.activeInHierarchy}, headset mirror {UnityEngine.XR.XRSettings.gameViewRenderMode}.");
            }
            string Mode = Vars.Config.StreamerView.Overlay;
            bool On = Mode != "Off";
            float Now = Time.unscaledTime;
            UpdateGaze(Cam, On && (Mode == "Always" || Now - LastParryTime < 1f));
            UpdateParry(Cam, On ? Now - LastParryTime : 10f);
            float W = Now - LastWarningTime;
            SetText(WarningText, On && W < .3f ? "PARRY" : null, new Color(1f, .85f, .2f, 1f - W / .3f));
            bool Triggers = Mode == "Always";
            string G = Triggers ? Feedback.AdaptiveTriggers.Describe(Gun: true) : null, A = Triggers ? Feedback.AdaptiveTriggers.Describe(Gun: false) : null;
            bool Left = Vars.Config.Controllers.LeftHanded;
            SetText(GunText, G != null ? (Left ? "L2  " : "R2  ") + G : null, new Color(1, 1, 1, .85f));
            SetText(ArmText, A != null ? (Left ? "R2  " : "L2  ") + A : null, new Color(1, 1, 1, .85f));
        }

        // The headset mirror is copied onto the window after the overlay canvases are drawn, so with it on the window
        // showed only the mirror: the picture, HUD and overlays were drawn and then covered. While the streamer view
        // shows, the mirror copy is off; it comes back when the view is turned off.
        private bool MirrorOff; private UnityEngine.XR.GameViewRenderMode SavedMirror;
        private void SetMirror(bool Mirror)
        {
            if (Mirror == !MirrorOff) return;
            try
            {
                if (!Mirror) { SavedMirror = UnityEngine.XR.XRSettings.gameViewRenderMode; UnityEngine.XR.XRSettings.gameViewRenderMode = UnityEngine.XR.GameViewRenderMode.None; }
                else UnityEngine.XR.XRSettings.gameViewRenderMode = SavedMirror;
            }
            catch (System.Exception E) { Vars.Log.LogWarning("Streamer screen: the headset mirror setting could not be changed: " + E.Message); }
            MirrorOff = !Mirror;
        }

        // ---- HUD ----
        private void UpdateHud()
        {
            float Scale;
            switch (Vars.Config.StreamerView.Hud)
            {
                case "Off": Scale = 0f; break;
                case "Small": Scale = .75f; break;
                case "Large": Scale = 1.35f; break;
                default: Scale = 1f; break;
            }
            NewMovement NM = Vars.Registered<NewMovement>();
            bool ShowHud = Scale > 0f && NM != null && !Vars.IsMainMenu;
            Hud.gameObject.SetActive(ShowHud);
            StyleHUD Style = ShowHud ? Vars.Registered<StyleHUD>() : null;
            bool ShowRank = Style != null && Style.ranks != null && Style.ranks.Count > 0 && (Style.comboActive || Style.forceMeterOn);
            Rank.gameObject.SetActive(ShowRank);
            if (!ShowHud) return;
            Hud.localScale = Rank.localScale = Vector3.one * Scale;
            Fill(HealthFill, NM.hp / 100f);
            HealthText.text = NM.hp.ToString();
            for (int i = 0; i < 3; i++) Fill(Stamina[i], (NM.boostCharge - i * 100f) / 100f);
            if (!ShowRank) return;
            StyleRank R = Style.currentRank;
            if (RankImage.sprite != R.sprite) RankImage.sprite = R.sprite;
            RankImage.enabled = R.sprite != null;
            Fill(RankFill, R.maxMeter > 0 ? Style.currentMeter / R.maxMeter : 0f);
        }

        // ---- Gaze ----
        private void UpdateGaze(Camera Cam, bool Show)
        {
            float Dt = Time.unscaledDeltaTime, Now = Time.unscaledTime;
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
                    // in picture pixels (1920x1080), so distances are the same across and down
                    Vector2 P = new Vector2(V.x * 1920f, V.y * 1080f);
                    if (!HaveGaze || (P - Gaze).magnitude > 1920f * .04f) Gaze = P; // jump with a saccade
                    else Gaze = Vector2.Lerp(Gaze, P, 1f - Mathf.Pow(.5f, Dt / .04f)); // rest during a fixation
                    HaveGaze = true;
                }
                else Fresh = false;
            }
            GazeAlpha = Mathf.MoveTowards(GazeAlpha, Fresh && Show ? 1f : 0f, Dt / .2f);
            if (HaveGaze && Fresh) TrailPoints.Enqueue((Gaze, Now));
            while (TrailPoints.Count > 0 && Now - TrailPoints.Peek().T > TrailSeconds) TrailPoints.Dequeue();
            while (TrailPoints.Count > TrailDots) TrailPoints.Dequeue();

            int i = 0;
            foreach (var (P, T) in TrailPoints)
            {
                Place(Trail[i], P, new Color(1, 1, 1, (1f - (Now - T) / TrailSeconds) * .25f * GazeAlpha), 1f);
                i++;
            }
            for (; i < TrailDots; i++) Trail[i].enabled = false;
            if (!HaveGaze || GazeAlpha <= 0f) { RingImage.enabled = false; return; }
            float Age = Now - LastParryTime;
            float Pulse = Age < .5f && LastParryByEyes ? 1f + (1f - Age / .5f) * 1.2f : 1f;
            Place(RingImage, Gaze, new Color(1, 1, 1, .5f * GazeAlpha), Pulse);
        }

        // ---- Parry: the path from the arm hand to where it was sent ----
        private void UpdateParry(Camera Cam, float Age)
        {
            bool Show = Age < .8f;
            float A = 1f - Age / .8f;
            Vector3 F = Cam.WorldToViewportPoint(LastParryFrom), T = Cam.WorldToViewportPoint(LastParryTo);
            bool Line = Show && F.z > 0f && T.z > 0f;
            for (int i = 0; i < PathDots; i++)
            {
                if (!Line) { Path[i].enabled = false; continue; }
                Vector2 P = Vector2.Lerp(new Vector2(F.x * 1920f, F.y * 1080f), new Vector2(T.x * 1920f, T.y * 1080f), i / (float)(PathDots - 1));
                Place(Path[i], P, new Color(1f, .55f, .1f, A * .9f), 1f);
            }
            SetText(ParryText, Show && LastParryByEyes ? "EYE-AIMED PARRY" : null, new Color(1f, .55f, .1f, A));
        }

        // ---- Building and placing ----
        // P in picture pixels (1920x1080 from the bottom left), put at the same spot of the letterboxed picture
        private void Place(Image I, Vector2 P, Color C, float Scale)
        {
            I.enabled = C.a > .005f;
            if (!I.enabled) return;
            RectTransform R = I.rectTransform;
            R.anchorMin = R.anchorMax = new Vector2(Mathf.Clamp01(P.x / 1920f), Mathf.Clamp01(P.y / 1080f));
            R.anchoredPosition = Vector2.zero;
            R.localScale = Vector3.one * Scale;
            I.color = C;
        }

        private static void Fill(RectTransform R, float Amount) => R.anchorMax = new Vector2(Mathf.Clamp01(Amount), 1f);

        private static void SetText(TMP_Text T, string S, Color C)
        {
            bool On = S != null && C.a > .005f;
            if (T.enabled != On) T.enabled = On;
            if (!On) return;
            if (T.text != S) T.text = S;
            T.color = C;
        }

        private Image Marker(Sprite S, float Size)
        {
            Image I = new GameObject("Marker", typeof(RectTransform)).AddComponent<Image>();
            I.transform.SetParent(View, false);
            I.sprite = S; I.raycastTarget = false; I.enabled = false;
            I.rectTransform.sizeDelta = new Vector2(Size, Size);
            return I;
        }

        private static RectTransform Rect(Transform Parent, string Name, Vector2 AnchorMin, Vector2 AnchorMax, Vector2 Position, Vector2 Size)
        {
            RectTransform R = new GameObject(Name, typeof(RectTransform)).GetComponent<RectTransform>();
            R.SetParent(Parent, false);
            R.anchorMin = AnchorMin; R.anchorMax = AnchorMax;
            R.pivot = new Vector2(AnchorMin.x == AnchorMax.x ? AnchorMin.x : .5f, AnchorMin.y == AnchorMax.y ? AnchorMin.y : .5f);
            R.anchoredPosition = Position; R.sizeDelta = Size;
            return R;
        }

        private static RectTransform Box(Transform Parent, string Name, Vector2 AnchorMin, Vector2 AnchorMax, Color C, Vector2 Position = default, Vector2 Size = default)
        {
            RectTransform R = Rect(Parent, Name, AnchorMin, AnchorMax, Position, Size);
            if (AnchorMin != AnchorMax) { R.pivot = Vector2.zero; R.offsetMin = R.offsetMax = Vector2.zero; R.anchoredPosition = Position; R.sizeDelta = Size; }
            Image I = R.gameObject.AddComponent<Image>();
            I.color = C; I.raycastTarget = false;
            return R;
        }

        private static TMP_Text Text(Transform Parent, string Name, float Size, TextAlignmentOptions Align)
        {
            RectTransform R = Rect(Parent, Name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI T = R.gameObject.AddComponent<TextMeshProUGUI>();
            T.fontSize = Size; T.alignment = Align; T.fontStyle = FontStyles.Bold;
            T.raycastTarget = false; T.enableWordWrapping = false; T.enabled = false;
            return T;
        }

        // the game's own font, so the overlay reads like ULTRAKILL; TextMeshPro's default otherwise
        private static TMP_FontAsset CachedFont;
        private static TMP_FontAsset GameFont()
        {
            if (CachedFont != null) return CachedFont;
            foreach (TMP_Text T in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (T != null && T.font != null && !(T.transform.root.GetComponent<MonitorOnly>() != null)) return CachedFont = T.font;
            return CachedFont = TMP_Settings.defaultFontAsset;
        }

        // a white ring (or disc, from 0) with a dark edge on both sides, so it shows on bright and dark scenes
        private static Texture2D RingTexture(int Size, float Inner, float Outer)
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
