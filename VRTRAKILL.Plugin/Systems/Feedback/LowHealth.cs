using UnityEngine;

namespace Plugin.Systems.Feedback
{
    // A heartbeat when health is low. In VR the health bar sits on the wrist, out of sight in a fight, and the game's
    // low health cues are on a flat screen; the PS VR2 tester wanted to feel it "when I'm close to dying". Below 30
    // health both hands beat (a strong beat, then a softer one), faster and stronger as health drops: about 65 beats a
    // minute at 30, 110 near death. With "Screen flashes" on, the edge of the view also takes a dark red glow that
    // swells with each beat: slow (under 2 a second), at the edge, dim, so it is not a flash. "Low health warning" in
    // the comfort settings turns both off.
    internal static class LowHealth
    {
        private const int From = 30;
        private static float NextBeat, SecondBeat = -1f, LastBeat = -10f;

        // How much red the edge of the view shows now (0 to about 0.4); read by ComfortVignette.
        public static float EdgeAlpha { get; private set; }

        // Called once a frame, before the vibrations are sent.
        public static void Tick()
        {
            EdgeAlpha = 0f;
            if (!Vars.Config.Comfort.LowHealthWarning || Vars.IsMainMenu || Vars.IsPaused) { NextBeat = 0f; return; }
            NewMovement NM = Vars.Registered<NewMovement>();
            if (NM == null || NM.dead || NM.hp <= 0 || NM.hp > From) { NextBeat = 0f; return; }

            float Danger = Mathf.InverseLerp(From, 1, NM.hp); // 0 at 30 health, 1 at 1
            float Now = Time.unscaledTime;
            if (Now >= NextBeat)
            {
                Haptics.Add(Haptics.Hand.Both, Mathf.Lerp(.3f, .6f, Danger), .07f, Haptics.Thud, "heartbeat");
                SecondBeat = Now + .2f;
                LastBeat = Now;
                NextBeat = Now + Mathf.Lerp(.92f, .55f, Danger);
            }
            if (SecondBeat > 0f && Now >= SecondBeat)
            {
                Haptics.Add(Haptics.Hand.Both, Mathf.Lerp(.18f, .35f, Danger), .05f, Haptics.Thud, "heartbeat");
                SecondBeat = -1f;
            }

            if (UI.FlashLimiter.Cap > 0f)
            {
                // swells over 0.15 s after a beat and fades over the next half second
                float Since = Now - LastBeat;
                float Swell = Since < .15f ? Since / .15f : Mathf.Exp(-(Since - .15f) * 4f);
                EdgeAlpha = Mathf.Lerp(.12f, .3f, Danger) * (.55f + .45f * Swell);
            }
        }
    }
}
