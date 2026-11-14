using System.Collections.Generic;
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
        private Graphic[] AlwaysOn;
        private readonly List<Image> Trail = new List<Image>(), Path = new List<Image>();
        private TMP_Text HealthText, ParryText, WarningText, GunText, ArmText, FreshText, FeedText;
        private Image WeaponImage;
        private RectTransform RankBack; private string LaidOutFeed = ""; private bool LaidOutFresh = true;
        private WeaponHUD WeaponSource; private Image WeaponSourceIcon;
        // the headset comparison: the raw headset picture, and in Side by side the smoothed one beside it
        private RectTransform HeadsetFrame, SmoothFrame; private RawImage HeadsetImage, SmoothImage;
        private TMP_Text HeadsetLabel, SmoothLabel; private string LaidOutCompare;
        // a trigger label shows when it changes (a charge step, READY, LOCKED, another weapon) and then fades
        private string GunShown, ArmShown; private float GunChanged = -10f, ArmChanged = -10f;
        private bool FontSet;
        private int LoggedScene, FramesInScene;
        private Vector2 Gaze; private bool HaveGaze; private float GazeAlpha;
        private readonly Queue<(Vector2 P, float T)> TrailPoints = new Queue<(Vector2, float)>();

        // A child of the mod's hidden host object: the game destroys visible objects kept across scenes shortly after it
        // starts (see ModHost), and took this canvas with them, so the monitor never showed the picture.
        public static void Create(GameObject Host)
        {
            if (Instance != null) return;
            HostObject = Host;
            GameObject G = new GameObject("VRTRAKILL Streamer Screen", typeof(RectTransform));
            G.transform.SetParent(Host.transform, false);
            G.AddComponent<MonitorOnly>(); // before the canvas exists, so no conversion pass can take it
            Instance = G.AddComponent<StreamerOverlay>();
        }

        private void Awake()
        {
            try { Build(); }
            catch (System.Exception E) { Vars.Log.LogError("Streamer screen could not be made: " + E); }
        }
        private static GameObject HostObject;

        // Called every frame by StreamerView, which keeps running. Something in the game turns this object off as it
        // starts (right after BepInEx's "Chainloader startup complete"), so the monitor never showed the picture; it is
        // turned back on, or made again if it is gone, and the first time is logged with what was found.
        public static void KeepAlive()
        {
            if (HostObject == null) return;
            if (Instance == null) { Create(HostObject); Note("gone, made again"); return; }
            if (!Instance.gameObject.activeSelf) { Instance.gameObject.SetActive(true); Note("its object was turned off"); }
            if (!Instance.enabled) { Instance.enabled = true; Note("it was disabled"); }
            // the same pass turns off the canvas scaler, and the HUD sizes are for its 1920x1080 reference
            foreach (Behaviour B in Instance.GetComponents<Behaviour>())
                if (!B.enabled && !(B is Canvas)) { B.enabled = true; Note(B.GetType().Name + " was disabled"); }
            // and the images inside it: the background, the picture and the HUD bars (a test capture of the window had
            // the gaze ring, which is turned on every frame, but no HUD)
            if (Instance.AlwaysOn == null) return;
            int Off = 0;
            foreach (Graphic G in Instance.AlwaysOn)
                if (G != null && !G.enabled) { G.enabled = true; Off++; }
            if (Off > 0) Note("images", Off + " of its " + Instance.AlwaysOn.Length + " picture and HUD images were disabled");
        }
        // each kind once, so a pass that runs again on every scene load does not fill the log
        private static readonly HashSet<string> Noted = new HashSet<string>();
        private static void Note(string Kind, string What = null)
        {
            if (!Noted.Add(Kind)) return;
            Vars.Log.LogInfo("Streamer screen: turned back on (" + (What ?? Kind) + ").");
        }

        private void Build()
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
            View.anchorMin = View.anchorMax = View.pivot = new Vector2(.5f, .5f); // sized every frame: Letterbox

            // the headset comparison, made before the HUD so the HUD stays on top (UpdateComparison)
            HeadsetFrame = Frame("Headset", out HeadsetImage, out HeadsetLabel);
            SmoothFrame = Frame("Streamer", out SmoothImage, out SmoothLabel);

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
            // the weapon in hand, right of the health bar, in its variation's colour (blue, green, red...); the
            // game's glow sprite is left out: drawn over the icon in this box it showed the weapon twice
            WeaponImage = new GameObject("Weapon", typeof(RectTransform)).AddComponent<Image>();
            WeaponImage.transform.SetParent(Hud, false);
            RectTransform WR = WeaponImage.rectTransform;
            WR.anchorMin = WR.anchorMax = WR.pivot = Vector2.zero; WR.anchoredPosition = new Vector2(292, -4); WR.sizeDelta = new Vector2(76, 76);
            WeaponImage.preserveAspect = true; WeaponImage.raycastTarget = false; WeaponImage.enabled = false;
            // style rank, top right
            Rank = Rect(View, "Rank", Vector2.one, Vector2.one, new Vector2(-30, -30), new Vector2(260, 100));
            // a grey backdrop behind the rank, meter, freshness and bonuses, sized to them (LayoutRankBack): white text
            // over a bright sky could not be read (PS VR2 tester)
            RankBack = Box(Rank, "Backdrop", Vector2.zero, Vector2.one, new Color(.15f, .15f, .15f, .65f));
            RankImage = new GameObject("Sprite", typeof(RectTransform)).AddComponent<Image>();
            RankImage.transform.SetParent(Rank, false);
            RankImage.preserveAspect = true; RankImage.raycastTarget = false;
            RectTransform RI = RankImage.rectTransform; RI.anchorMin = new Vector2(0, .25f); RI.anchorMax = Vector2.one; RI.sizeDelta = Vector2.zero;
            RectTransform MeterBack = Box(Rank, "Meter", new Vector2(0, 0), new Vector2(1, 0), new Color(0, 0, 0, .55f), Vector2.zero, new Vector2(0, 10));
            RankFill = Box(MeterBack, "Fill", Vector2.zero, new Vector2(1, 1), new Color(1, 1, 1, .85f));
            // under the meter: the weapon freshness ("FRESH: 1.50x"), then the style bonuses as the game lists them
            // (+ PARRY, + HEADSHOT...), newest on top: in VR the game's own panel floats beside the view, and is left
            // out of the streamer picture while this HUD shows (StreamerView)
            FreshText = Text(Rect(Rank, "Freshness", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -34), new Vector2(0, 28)), "Text", 22, TextAlignmentOptions.Right);
            FeedText = Text(Rect(Rank, "Bonuses", new Vector2(0, 0), new Vector2(1, 0), new Vector2(-40, -290), new Vector2(80, 250)), "Text", 24, TextAlignmentOptions.TopRight);
            FeedText.overflowMode = TextOverflowModes.Truncate;

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

            // what KeepAlive turns back on: everything made enabled, except the rank sprite, shown only with a sprite
            var On = new List<Graphic>();
            foreach (Graphic G in GetComponentsInChildren<Graphic>(true))
                if (G.enabled && G != RankImage) On.Add(G);
            AlwaysOn = On.ToArray();
        }

        private void LateUpdate()
        {
            bool Show = StreamerView.Showing;
            if (Canvas.enabled != Show) Canvas.enabled = Show;
            SetMirror(!Show);
            if (!Show) return;
            Picture.texture = StreamerView.Picture;
            Letterbox();
            bool SideBySide = UpdateComparison();
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
            int Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (LoggedScene != Scene && ++FramesInScene > 60)
            {
                // once a scene, after a second: whether the monitor canvas is what it should be, for a test log
                LoggedScene = Scene; FramesInScene = 0;
                Vars.Log.LogInfo($"Streamer screen: canvas {Canvas.renderMode}, enabled {Canvas.isActiveAndEnabled}, converted to VR {GetComponent<UI.UICanvas>() != null}, "
                    + $"picture {View.rect.width:0}x{View.rect.height:0} of {Screen.width}x{Screen.height}, texture {(Picture.texture != null ? Picture.texture.width + "x" + Picture.texture.height : "none")}, HUD shown {Hud.gameObject.activeInHierarchy}, headset mirror {Unity.XR.OpenVR.OpenVRSettings.GetSettings()?.GetMirrorViewMode()}, Unity game view {UnityEngine.XR.XRSettings.gameViewRenderMode}.");
            }
            string Mode = Vars.Config.StreamerView.Overlay;
            // side by side, the pictures are half size and the gaze and parry marks would land in the wrong place
            bool On = Mode != "Off" && !SideBySide;
            float Now = Time.unscaledTime;
            UpdateGaze(Cam, On && (Mode == "Always" || Now - LastParryTime < 1f));
            UpdateParry(Cam, On ? Now - LastParryTime : 10f);
            float W = Now - LastWarningTime;
            SetText(WarningText, On && W < .3f ? "PARRY" : null, new Color(1f, .85f, .2f, 1f - W / .3f));
            bool Triggers = Mode == "Always";
            string G = Triggers ? Feedback.AdaptiveTriggers.Describe(Gun: true) : null, A = Triggers ? Feedback.AdaptiveTriggers.Describe(Gun: false) : null;
            bool Left = Vars.Config.Controllers.LeftHanded;
            // a label that stayed on screen ("R2 CLICK" at rest) only added noise to a video
            SetText(GunText, G != null ? (Left ? "L2  " : "R2  ") + G : null, new Color(1, 1, 1, .85f * Recent(G, ref GunShown, ref GunChanged, Now)));
            SetText(ArmText, A != null ? (Left ? "R2  " : "L2  ") + A : null, new Color(1, 1, 1, .85f * Recent(A, ref ArmShown, ref ArmChanged, Now)));
        }

        // ---- Headset comparison ----
        // Corner: the raw headset picture in a framed corner at the top left, over the smoothed view. Side by side: the
        // raw picture on the left and the smoothed one on the right, each labelled; the full-size picture is made
        // transparent (it stays enabled for KeepAlive). Returns whether it is side by side.
        private bool UpdateComparison()
        {
            string Mode = Vars.Config.StreamerView.Comparison;
            RenderTexture Raw = StreamerView.HeadsetPicture;
            bool Corner = Mode == "Corner" && Raw != null, Side = Mode == "Side by side" && Raw != null;
            HeadsetImage.texture = Raw; SmoothImage.texture = StreamerView.Picture;
            if (HeadsetFrame.gameObject.activeSelf != (Corner || Side)) HeadsetFrame.gameObject.SetActive(Corner || Side);
            if (SmoothFrame.gameObject.activeSelf != Side) SmoothFrame.gameObject.SetActive(Side);
            Color Main = Side ? Color.clear : Color.white;
            if (Picture.color != Main) Picture.color = Main;
            SetText(HeadsetLabel, Corner ? "HEADSET" : Side ? "HEADSET (RAW)" : null, Color.white);
            SetText(SmoothLabel, Side ? "STREAMER VIEW" : null, Color.white);
            string Layout = Corner ? "Corner" : Side ? "Side" : "Off";
            if (Layout != LaidOutCompare)
            {
                LaidOutCompare = Layout;
                if (Corner) Put(HeadsetFrame, new Vector2(0, 1), new Vector2(24, -24), new Vector2(584, 332)); // a 576x324 picture
                if (Side)
                {
                    Put(HeadsetFrame, new Vector2(0, .5f), Vector2.zero, new Vector2(956, 540));
                    Put(SmoothFrame, new Vector2(1, .5f), Vector2.zero, new Vector2(956, 540));
                }
            }
            return Side;
        }

        private static void Put(RectTransform R, Vector2 Anchor, Vector2 Position, Vector2 Size)
        {
            R.anchorMin = R.anchorMax = R.pivot = Anchor;
            R.anchoredPosition = Position; R.sizeDelta = Size;
        }

        // a white-bordered picture with a label strip along its top, hidden until a comparison mode shows it
        private RectTransform Frame(string Name, out RawImage Image, out TMP_Text Label)
        {
            RectTransform F = Box(View, Name + " frame", new Vector2(0, 1), new Vector2(0, 1), new Color(1, 1, 1, .9f));
            Image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            Image.transform.SetParent(F, false);
            Image.raycastTarget = false;
            RectTransform R = Image.rectTransform;
            R.anchorMin = Vector2.zero; R.anchorMax = Vector2.one; R.offsetMin = new Vector2(4, 4); R.offsetMax = new Vector2(-4, -4);
            RectTransform Strip = Box(F, "Label", new Vector2(0, 1), new Vector2(1, 1), new Color(0, 0, 0, .6f), new Vector2(4, -40), new Vector2(-8, 36));
            Label = Text(Strip, "Text", 24, TextAlignmentOptions.Center);
            F.gameObject.SetActive(false);
            return F;
        }

        // 1 for 1.5 s after the label changed, then fading out over 0.4 s
        private static float Recent(string Label, ref string Shown, ref float Changed, float Now)
        {
            if (Label != Shown) { Shown = Label; Changed = Now; }
            return Mathf.Clamp01(1f - (Now - Changed - 1.5f) / .4f);
        }

        // The OpenVR plugin copies the headset's right eye onto the window after the overlay canvases are drawn, so the
        // window showed only that mirror: the picture, HUD and overlays were drawn and then covered. While the streamer
        // view shows, the plugin's own mirror (OpenVRSettings.MirrorView) is off; it comes back when the view is turned
        // off. (Unity's XRSettings.gameViewRenderMode is not the plugin's switch: setting it to None instead made Unity
        // draw this canvas into the right eye, where the PS VR2 tester saw the gaze ring.)
        private bool MirrorOff; private Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes SavedMirror;
        private void SetMirror(bool Mirror)
        {
            if (Mirror == !MirrorOff) return;
            try
            {
                Unity.XR.OpenVR.OpenVRSettings Settings = Unity.XR.OpenVR.OpenVRSettings.GetSettings();
                if (Settings == null) return;
                if (!Mirror) { SavedMirror = Settings.GetMirrorViewMode(); Settings.SetMirrorViewMode(Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.None); }
                else Settings.SetMirrorViewMode(SavedMirror);
            }
            catch (System.Exception E) { Vars.Log.LogWarning("Streamer screen: the headset mirror setting could not be changed: " + E.Message); }
            MirrorOff = !Mirror;
        }

        // The largest 16:9 rectangle that fits the canvas, centred. (An AspectRatioFitter sized it from the wrong parent
        // size, 4164x4228 on a 3840x2160 screen, so the monitor showed a zoomed-in crop.)
        private void Letterbox()
        {
            Rect Area = ((RectTransform)transform).rect;
            if (Area.width <= 0f || Area.height <= 0f) return;
            Vector2 Size = Area.width / Area.height > 16f / 9f ? new Vector2(Area.height * 16f / 9f, Area.height) : new Vector2(Area.width, Area.width * 9f / 16f);
            if (View.sizeDelta != Size) View.sizeDelta = Size;
            View.anchoredPosition = Vector2.zero;
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
            SetText(HealthText, NM.hp.ToString(), Color.white); // made turned off, like every label
            for (int i = 0; i < 3; i++) Fill(Stamina[i], (NM.boostCharge - i * 100f) / 100f);
            UpdateWeapon();
            if (!ShowRank) return;
            StyleRank R = Style.currentRank;
            if (RankImage.sprite != R.sprite) RankImage.sprite = R.sprite;
            RankImage.enabled = R.sprite != null;
            Fill(RankFill, R.maxMeter > 0 ? Style.currentMeter / R.maxMeter : 0f);
            TMP_Text Fresh = Style.freshnessSliderText, Feed = Style.styleInfo;
            SetText(FreshText, Fresh != null && Fresh.gameObject.activeInHierarchy && !string.IsNullOrEmpty(Fresh.text) ? Fresh.text : null, Color.white);
            SetText(FeedText, Feed != null && !string.IsNullOrEmpty(Feed.text) ? Feed.text : null, Color.white);
            string FeedNow = FeedText.enabled ? FeedText.text : null;
            if (FeedNow != LaidOutFeed || FreshText.enabled != LaidOutFresh) LayoutRankBack(FeedNow, FreshText.enabled);
        }

        // Sizes the backdrop to what shows under the rank: the freshness line, and the bonus list (as tall and as wide
        // as its lines, which can be wider than the rank). Only when they change, since measuring text costs.
        private void LayoutRankBack(string Feed, bool Fresh)
        {
            LaidOutFeed = Feed; LaidOutFresh = Fresh;
            float Below = Fresh ? 36f : 4f, Wider = 0f;
            if (Feed != null)
            {
                Vector2 Size = FeedText.GetPreferredValues(Feed);
                Below = 40f + Mathf.Min(Size.y, 250f);
                Wider = Mathf.Clamp(Size.x - 260f, 0f, 80f);
            }
            RankBack.offsetMin = new Vector2(-14f - Wider, -Below - 10f);
            RankBack.offsetMax = new Vector2(14f, 10f);
        }

        // The weapon in hand, as the game's weapon HUD shows it (on the wrist in VR): its icon, tinted with the
        // variation's colour. Hidden with no weapon out.
        private void UpdateWeapon()
        {
            WeaponHUD W = Vars.Registered<WeaponHUD>();
            if (W != WeaponSource)
            {
                WeaponSource = W;
                WeaponSourceIcon = W != null ? W.GetComponent<Image>() : null;
            }
            GunControl Guns = Vars.Registered<GunControl>();
            bool Armed = Guns != null && !Guns.noWeapons && Guns.currentWeapon != null;
            Copy(WeaponImage, Armed ? WeaponSourceIcon : null);
        }
        private static void Copy(Image To, Image From)
        {
            bool On = From != null && From.sprite != null;
            if (To.enabled != On) To.enabled = On;
            if (!On) return;
            if (To.sprite != From.sprite) To.sprite = From.sprite;
            Color C = From.color; C.a = 1f;
            To.color = C;
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
