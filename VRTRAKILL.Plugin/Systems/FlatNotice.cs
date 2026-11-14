using UnityEngine;

namespace Plugin.Systems
{
    // When "Start in VR" is "Auto" and SteamVR was not running, the game starts flat with nothing of the mod applied.
    // This box at the top of the monitor says so for the first 20 seconds, and how to get VR, so a player who meant to
    // play in VR is not left with a plain flat game and no idea why. It names SteamVR only, which every headset uses.
    internal sealed class FlatNotice : MonoBehaviour
    {
        private const float Seconds = 20f, FadeSeconds = 2f;
        private const string Text = "VRTRAKILL: SteamVR was not running, so ULTRAKILL started without VR.\n"
                                  + "To play in VR, start SteamVR first, then ULTRAKILL.";
        private float ShownAt = -1f;
        private GUIStyle Style;

        public static void Show()
        {
            // hidden like ModHost, so the game's cleanup of objects kept across scenes does not find it
            GameObject G = new GameObject("VRTRAKILL Flat Notice") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(G);
            G.AddComponent<FlatNotice>();
        }

        private void OnGUI()
        {
            float Now = Time.realtimeSinceStartup;
            if (ShownAt < 0f) ShownAt = Now; // from the first frame drawn: the boot screen can take a while
            float Age = Now - ShownAt;
            if (Age > Seconds) { Destroy(gameObject); return; }

            if (Style == null)
            {
                Style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
                Style.normal.textColor = Color.white;
            }
            Style.fontSize = Mathf.Clamp(Screen.height / 45, 14, 36);
            float Width = Mathf.Min(Screen.width - 40, Style.fontSize * 44);
            float Height = Style.CalcHeight(new GUIContent(Text), Width) + Style.fontSize;
            float Alpha = Mathf.Clamp01((Seconds - Age) / FadeSeconds);

            Color Background = GUI.backgroundColor, Content = GUI.contentColor;
            GUI.backgroundColor = new Color(0, 0, 0, .9f * Alpha);
            GUI.contentColor = new Color(1, 1, 1, Alpha);
            GUI.Box(new Rect((Screen.width - Width) / 2, 20, Width, Height), Text, Style);
            GUI.backgroundColor = Background; GUI.contentColor = Content;
        }
    }
}
