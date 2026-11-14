using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.UI
{
    // Caps the full-view white (parry) and red (damage) flashes. In VR the mod stretches them over the whole view, and
    // in a fight they come several times a second: game accessibility guidance (Xbox guideline 118, WCAG 2.3.1) flags
    // flashes over about a fifth of the view more than three times a second, red ones most of all. "Screen flashes" in
    // the comfort settings: "Reduced" (default) keeps them faint, "Off" hides them, "Full" leaves the game's look.
    // Fades to black are not touched: they show level changes and deaths. Clamping in LateUpdate, after the game's own
    // scripts set the colour in Update, works whoever made the flash.
    internal sealed class FlashLimiter : MonoBehaviour
    {
        private const float ReducedAlpha = .25f;
        private Image Img;

        public static void Attach(Component Flash)
        {
            if (Flash == null || Flash.GetComponent<Image>() == null || Flash.GetComponent<FlashLimiter>() != null) return;
            Flash.gameObject.AddComponent<FlashLimiter>();
        }

        // The brightest the flash may get with the current setting; 1 leaves it as it is.
        public static float Cap
        {
            get
            {
                string Mode = Vars.Config.Comfort.ScreenFlashes ?? "Reduced";
                if (Mode.Equals("Full", System.StringComparison.OrdinalIgnoreCase)) return 1f;
                if (Mode.Equals("Off", System.StringComparison.OrdinalIgnoreCase)) return 0f;
                return ReducedAlpha;
            }
        }

        private void Awake() => Img = GetComponent<Image>();

        private void LateUpdate()
        {
            if (Img == null) return;
            float Max = Cap;
            Color C = Img.color;
            if (C.a > Max) { C.a = Max; Img.color = C; }
        }
    }
}
