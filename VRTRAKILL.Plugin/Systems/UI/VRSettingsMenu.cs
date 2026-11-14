using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.UI
{
    // A "VR" column beside the pause menu, so the main settings can be changed in the headset instead of in
    // VRTRAKILL_Config.json. It is made of copies of the pause menu's own buttons, so it looks like the game. Each button
    // shows a setting and its value; pressing it moves to the next value, applies it at once and saves the config.
    internal sealed class VRSettingsMenu : MonoBehaviour
    {
        private sealed class Item
        {
            public Func<string> Text;
            public Action Next;
            public TMP_Text Label;
        }

        private GameObject PauseMenu;
        private RectTransform Column;
        private CanvasGroup Group;
        private readonly List<Item> Items = new List<Item>();
        // left handed is read when VR starts and when the controllers are made
        private static readonly bool LeftHandedAtStart = Vars.Config.Controllers.LeftHanded;

        // Adds the column to the pause menu once.
        public static void Attach(OptionsManager OM)
        {
            if (OM == null || OM.pauseMenu == null || OM.GetComponent<VRSettingsMenu>() != null) return;
            VRSettingsMenu M = OM.gameObject.AddComponent<VRSettingsMenu>();
            M.PauseMenu = OM.pauseMenu;
            try { M.Build(); }
            catch (Exception E) { Vars.Log.LogWarning("The VR settings column could not be added to the pause menu: " + E.Message); Destroy(M); }
        }

        private void Build()
        {
            Button Template = null;
            foreach (Button B in PauseMenu.GetComponentsInChildren<Button>(true))
                if (B.GetComponentInChildren<TMP_Text>(true) != null) { Template = B; break; }
            if (Template == null) throw new InvalidOperationException("no pause menu button to copy");

            RectTransform Menu = (RectTransform)PauseMenu.transform;
            RectTransform ButtonRect = (RectTransform)Template.transform;
            Column = new GameObject("VR Settings", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            Column.SetParent(Menu.parent, false);
            Column.gameObject.layer = PauseMenu.layer;
            Column.anchorMin = Menu.anchorMin; Column.anchorMax = Menu.anchorMax; Column.pivot = Menu.pivot;
            // not the menu's scale: it opens with an animation (HudOpenEffect) that starts from zero, and copying it
            // then made the column too small to see or point at
            Column.localScale = Vector3.one; Column.localRotation = Quaternion.identity;
            Group = Column.GetComponent<CanvasGroup>();

            AddItems();

            float Height = ButtonRect.rect.height, Gap = Height * .25f;
            float Width = ButtonRect.rect.width * 1.35f;
            Column.sizeDelta = new Vector2(Width + Gap * 2, (Height + Gap) * (Items.Count + 1) + Gap);
            // to the right of the pause menu, top edges level
            Column.anchoredPosition = Menu.anchoredPosition + new Vector2((Menu.rect.width + Column.sizeDelta.x) * .5f + Gap * 2, (Menu.rect.height - Column.sizeDelta.y) * .5f);

            // the pause menu's background, if it has one
            if (PauseMenu.GetComponent<Image>() is Image Back)
            {
                Image I = Column.gameObject.AddComponent<Image>();
                I.sprite = Back.sprite; I.type = Back.type; I.color = Back.color; I.material = Back.material; I.pixelsPerUnitMultiplier = Back.pixelsPerUnitMultiplier;
            }

            // title, from the button's own text style
            TMP_Text Title = Instantiate(Template.GetComponentInChildren<TMP_Text>(true).gameObject, Column, false).GetComponent<TMP_Text>();
            RectTransform TR = (RectTransform)Title.transform;
            TR.anchorMin = TR.anchorMax = new Vector2(.5f, 1f); TR.pivot = new Vector2(.5f, 1f);
            TR.sizeDelta = new Vector2(Width, Height); TR.anchoredPosition = new Vector2(0, -Gap); TR.localScale = Vector3.one;
            foreach (MonoBehaviour B in Title.GetComponents<MonoBehaviour>()) if (!(B is Graphic)) DestroyImmediate(B);
            Title.text = "VR"; Title.alignment = TextAlignmentOptions.Center;

            for (int i = 0; i < Items.Count; i++)
            {
                Item It = Items[i];
                GameObject G = Instantiate(Template.gameObject, Column, false);
                G.name = "VR Setting " + i;
                G.SetActive(true);
                StripBehaviours(G);
                RectTransform R = (RectTransform)G.transform;
                R.anchorMin = R.anchorMax = new Vector2(.5f, 1f); R.pivot = new Vector2(.5f, 1f);
                R.sizeDelta = new Vector2(Width, Height);
                R.anchoredPosition = new Vector2(0, -Gap - (Height + Gap) * (i + 1));
                R.localScale = Vector3.one; R.localRotation = Quaternion.identity;
                foreach (Transform Child in R) Child.localScale = Vector3.one;
                Button Btn = G.GetComponent<Button>();
                // a new event: the copied one still holds the original button's listeners from the scene
                Btn.onClick = new Button.ButtonClickedEvent();
                Btn.onClick.AddListener(() => { It.Next(); Save(); Refresh(); });
                Btn.interactable = true;
                Navigation N = Btn.navigation; N.mode = Navigation.Mode.Automatic; Btn.navigation = N;
                It.Label = G.GetComponentInChildren<TMP_Text>(true);
                if (It.Label != null) { It.Label.enableAutoSizing = true; It.Label.fontSizeMax = It.Label.fontSize; It.Label.fontSizeMin = It.Label.fontSize * .5f; }
            }
            Refresh();
            UIConverter.ConvertCanvas(Column.GetComponentInParent<Canvas>());
        }

        private void AddItems()
        {
            var C = Vars.Config.Controllers;
            // first, so a new player finds it: sets turning, vignette, flashes and weapon shake together (Prefs/ComfortPresets)
            Items.Add(new Item
            {
                Text = () => "COMFORT: " + Prefs.ComfortPresets.Current(),
                Next = () => Prefs.ComfortPresets.Apply(Prefs.ComfortPresets.Next()),
            });
            Items.Add(new Item
            {
                Text = () => C.SnapTurn ? $"TURN: SNAP {C.SnapAngles:0}°" : "TURN: SMOOTH",
                Next = () =>
                {
                    if (!C.SnapTurn) { C.SnapTurn = true; C.SnapAngles = 30; }
                    else if (C.SnapAngles < 44) C.SnapAngles = 45;
                    else if (C.SnapAngles < 89) C.SnapAngles = 90;
                    else C.SnapTurn = false;
                },
            });
            Items.Add(new Item
            {
                Text = () => $"TURN SPEED: {C.SmoothSpeed:0}°/S",
                Next = () => C.SmoothSpeed = Step(C.SmoothSpeed, new[] { 120f, 180f, 240f, 300f, 360f }),
            });
            Items.Add(new Item
            {
                Text = () => Vars.Config.Comfort.Vignette ? "COMFORT VIGNETTE: ON" : "COMFORT VIGNETTE: OFF",
                Next = () => Vars.Config.Comfort.Vignette = !Vars.Config.Comfort.Vignette,
            });
            Items.Add(new Item
            {
                Text = () => "SCREEN FLASHES: " + (FlashLimiter.Cap >= 1f ? "FULL" : FlashLimiter.Cap <= 0f ? "OFF" : "REDUCED"),
                Next = () => Vars.Config.Comfort.ScreenFlashes = FlashLimiter.Cap >= 1f ? "Reduced" : FlashLimiter.Cap <= 0f ? "Full" : "Off",
            });
            Items.Add(new Item
            {
                Text = () => Vars.Config.StreamerView.On ? "STREAMER VIEW: ON" : "STREAMER VIEW: OFF",
                Next = () => VRCamera.StreamerView.Toggle(),
            });
            Items.Add(new Item
            {
                Text = () => "OVERLAY: " + Vars.Config.StreamerView.Overlay.ToUpperInvariant(),
                Next = () => Vars.Config.StreamerView.Overlay = Vars.Config.StreamerView.Overlay == "Always" ? "Parries" : Vars.Config.StreamerView.Overlay == "Parries" ? "Off" : "Always",
            });
            Items.Add(new Item
            {
                Text = () => "COMPARE: " + Vars.Config.StreamerView.Comparison.ToUpperInvariant(),
                Next = () => VRCamera.StreamerView.CycleComparison(),
            });
            Items.Add(new Item
            {
                Text = () => Vars.Config.UIInteraction.WristHud ? "HUD: ON WRIST" : "HUD: FLOATING",
                Next = () => Vars.Config.UIInteraction.WristHud = !Vars.Config.UIInteraction.WristHud,
            });
            var UII = Vars.Config.UIInteraction;
            Items.Add(new Item
            {
                Text = () => $"UI SIZE: {UII.UISize / .04f * 100f:0}%",
                Next = () => UII.UISize = Step(UII.UISize, new[] { .03f, .035f, .04f, .05f, .0625f }),
            });
            Items.Add(new Item
            {
                Text = () => C.EnableHaptics && C.RumbleStrength > 0f ? $"RUMBLE: {C.RumbleStrength * 100:0}%" : "RUMBLE: OFF",
                Next = () =>
                {
                    float S = !C.EnableHaptics ? 0f : Step(C.RumbleStrength, new[] { 0f, .5f, 1f, 1.5f, 2f });
                    C.RumbleStrength = S; C.EnableHaptics = S > 0f;
                    if (S == 0f) Feedback.Haptics.StopAll();
                    else Feedback.Haptics.Add(Feedback.Haptics.Hand.Both, .6f, .12f, Feedback.Haptics.Buzz, "rumble preview");
                },
            });
            Items.Add(new Item
            {
                Text = () => $"HEIGHT: {(Vars.Config.HeightOffset >= 0 ? "+" : "")}{Vars.Config.HeightOffset * 100:0} CM",
                Next = () =>
                {
                    Vars.Config.HeightOffset = Mathf.Round(Step(Vars.Config.HeightOffset, new[] { -.2f, -.15f, -.1f, -.05f, 0f, .05f, .1f, .15f, .2f }) * 100f) / 100f;
                    VRCamera.Patches.CameraConverterP.RequestHeightCalibration();
                },
            });
            Items.Add(new Item
            {
                Text = () => "RECENTER VIEW",
                Next = () => VRCamera.Patches.CameraConverterP.RequestHeightCalibration(),
            });
            Items.Add(new Item
            {
                Text = () => C.AdaptiveTriggers ? "TRIGGERS: ON" : "TRIGGERS: OFF",
                Next = () => C.AdaptiveTriggers = !C.AdaptiveTriggers,
            });
            Items.Add(new Item
            {
                Text = () => C.FingerTracking ? "FINGERS: ON" : "FINGERS: OFF",
                Next = () => C.FingerTracking = !C.FingerTracking,
            });
            Items.Add(new Item
            {
                Text = () => Vars.Config.EnableMBP ? "PUNCH: SWING" : "PUNCH: BUTTON",
                Next = () => Vars.Config.EnableMBP = !Vars.Config.EnableMBP,
            });
            Items.Add(new Item
            {
                Text = () => (C.LeftHanded ? "LEFT HANDED: ON" : "LEFT HANDED: OFF") + (C.LeftHanded != LeftHandedAtStart ? " (RESTART)" : ""),
                Next = () => C.LeftHanded = !C.LeftHanded,
            });
            Items.Add(new Item
            {
                Text = () => Vars.IsMainMenu ? "CHEATS: IN LEVELS" : Vars.Registered<CheatsController>() is CheatsController Ch && Ch.cheatsEnabled ? "CHEATS MENU" : "CHEATS: OFF",
                Next = OpenCheats,
            });
        }

        // The game opens its cheat menu (the sandbox's spawner arm and tools, noclip, ...) with Home, ~ or ` on a
        // keyboard or Select + right trigger on a gamepad, and turns cheats on with the Konami code: none of them exist on
        // VR controllers, so the sandbox could not be used in VR. With cheats on (always in the sandbox) this opens the
        // cheat menu in place of the pause menu, as the keys do; with cheats off it shows the game's consent screen, as
        // the code does (CheatsMenuP brings the pause menu back after it). Pausing again closes the cheat menu.
        private void OpenCheats()
        {
            // the main menu has the cheat objects but hides their parent: nothing could show there
            if (Vars.IsMainMenu) return;
            CheatsController Ch = Vars.Registered<CheatsController>();
            CheatsManager Manager = Vars.Registered<CheatsManager>();
            if (Ch == null) return;
            GameObject Shown;
            if (!Ch.cheatsEnabled)
            {
                if (Ch.consentScreen == null) return;
                Ch.consentScreen.SetActive(true);
                Shown = Ch.consentScreen;
            }
            else
            {
                if (Manager == null || Manager.cheatsManagerMenu == null) return;
                Manager.ShowMenu();
                Shown = Manager.cheatsManagerMenu;
            }
            // the pause menu goes away only when the cheat screen really shows
            if (!Shown.activeInHierarchy)
            {
                Shown.SetActive(false);
                Vars.Log.LogWarning($"Cheat menu: \"{Shown.name}\" cannot show here (a parent is hidden); the pause menu stays.");
                return;
            }
            PauseMenu.SetActive(false);
            ShowInVR(Shown);
        }

        // The cheat menu and the consent screen sit on a canvas that is inactive while the level loads, so the VR canvas
        // conversion (which scans active canvases) never saw it: opened, it drew only on the monitor and the headset
        // showed nothing ("the whole menu disappeared", PS VR2 test). It is converted when it opens. Logged, for tests.
        private static void ShowInVR(GameObject Shown)
        {
            Canvas C = Shown.GetComponentInParent<Canvas>();
            if (C != null) C = C.rootCanvas;
            if (C == null) { Vars.Log.LogWarning($"Cheat menu: \"{Shown.name}\" has no canvas."); return; }
            RenderMode Was = C.renderMode;
            if (Was == RenderMode.ScreenSpaceOverlay)
            {
                UIConverter.ConvertCanvas(C);
                VRTRAKILL.Utilities.Unity.RecursiveChangeLayer(C.gameObject, (int)Layers.UI);
            }
            Vars.Log.LogInfo($"Cheat menu: \"{Shown.name}\" on canvas \"{C.name}\", {Was}{(Was == RenderMode.ScreenSpaceOverlay ? ", converted for VR" : "")}, active {Shown.activeInHierarchy}.");
        }

        // A copied pause button can carry the game's own behaviours (event triggers, scripts that resume or quit on
        // click): keep only plain UI parts on the copy.
        private static void StripBehaviours(GameObject G)
        {
            foreach (MonoBehaviour B in G.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (B is Button || B is Graphic || B is LayoutElement || B is Shadow || B is ContentSizeFitter || B is LayoutGroup) continue;
                DestroyImmediate(B);
            }
        }

        // The next value after the current one, wrapping to the first.
        private static float Step(float Current, float[] Values)
        {
            for (int i = 0; i < Values.Length; i++)
                if (Current < Values[i] - .001f) return Values[i];
            return Values[0];
        }

        private void Refresh()
        {
            foreach (Item It in Items) if (It.Label != null) It.Label.text = It.Text();
        }

        private static void Save()
        {
            try { Prefs.ConfigJSON.Serialize(Prefs.ConfigJSON.Instance); }
            catch (Exception E) { Vars.Log.LogWarning("VR settings could not be saved: " + E.Message); }
        }

        // shown with the first pause page only (the options pages hide the pause menu)
        private void LateUpdate()
        {
            if (Column == null) return;
            bool Show = PauseMenu != null && PauseMenu.activeInHierarchy;
            if (Group.alpha != (Show ? 1f : 0f)) Group.alpha = Show ? 1f : 0f;
            Group.interactable = Group.blocksRaycasts = Show;
        }
    }
}
