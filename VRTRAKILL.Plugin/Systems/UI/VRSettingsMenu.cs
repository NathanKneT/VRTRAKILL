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
                Text = () => Vars.Config.EnableMBP ? "PUNCH: SWING" : "PUNCH: BUTTON",
                Next = () => Vars.Config.EnableMBP = !Vars.Config.EnableMBP,
            });
            Items.Add(new Item
            {
                Text = () => (C.LeftHanded ? "LEFT HANDED: ON" : "LEFT HANDED: OFF") + (C.LeftHanded != LeftHandedAtStart ? " (RESTART)" : ""),
                Next = () => C.LeftHanded = !C.LeftHanded,
            });
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
