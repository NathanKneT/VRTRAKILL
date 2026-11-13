using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace Plugin.Systems.Feedback
{
    // Controller vibration, one channel per hand. Two sources are mixed every frame:
    // - the game's own gamepad rumble (RumbleManager: gunfire, slide, punch, whiplash...), split by the hand each rumble
    //   belongs to (PatchControllerHaptics). Before, only the strongest rumble played, on one controller;
    // - short pulses the mod adds for VR: a tick when a shot or a punch hits, a stronger knock on a kill, a thud when
    //   the player is hurt, and ticks when the menu laser moves to another item or clicks (PatchHitFeedback, UIInteraction).
    // Each hand plays the strongest of what it has. Sent from ModHost.LateUpdate, so it also runs in menus and paused.
    internal static class Haptics
    {
        public enum Hand { Arm, Gun, Both, Left, Right }

        // frequencies in Hz: a body buzz, a deep thud for impacts, a crisp tick for confirmations
        public const float Buzz = 160f, Thud = 90f, Tick = 230f;

        private struct Pulse { public float End, Amplitude, Frequency; public bool Left, Right; }
        private static readonly List<Pulse> Pulses = new List<Pulse>();

        // the game's rumble this frame, per hand (PatchControllerHaptics), and the frame it was set
        private static float GameLeft, GameRight;
        private static int GameFrame = -1;
        private static float SentLeft = -1f, SentRight = -1f, SentFreqLeft, SentFreqRight;

        // last sent values, for the developer test hook
        public static float LastLeft => SentLeft;
        public static float LastRight => SentRight;
        public static readonly List<string> TestLog = new List<string>();
        public static bool Recording;

        private static bool LeftHanded => Vars.Config.Controllers.LeftHanded;
        public static bool ArmIsLeft => !LeftHanded;

        // Stores the game's rumble for each hand this frame.
        public static void SetGameRumble(float Left, float Right)
        {
            GameLeft = Left; GameRight = Right; GameFrame = Time.frameCount;
        }

        // Adds a short pulse. Amplitude 0..1 before the "Rumble strength" setting.
        public static void Add(Hand H, float Amplitude, float Seconds, float Frequency, string Why)
        {
            if (!Vars.Config.Controllers.EnableHaptics || Amplitude <= 0f) return;
            bool L, R;
            switch (H)
            {
                case Hand.Arm: L = ArmIsLeft; R = !L; break;
                case Hand.Gun: L = !ArmIsLeft; R = !L; break;
                case Hand.Left: L = true; R = false; break;
                case Hand.Right: L = false; R = true; break;
                default: L = R = true; break;
            }
            float A = Mathf.Clamp01(Amplitude * Vars.Config.Controllers.RumbleStrength);
            Pulses.Add(new Pulse { End = Time.unscaledTime + Seconds, Amplitude = A, Frequency = Frequency, Left = L, Right = R });
            if (Recording) TestLog.Add($"{Why} {(L && R ? "both" : L ? "left" : "right")} {A:0.00} {Frequency:0}Hz");
        }

        // Sends each hand's strongest vibration. Called once a frame.
        public static void Flush()
        {
            float Now = Time.unscaledTime;
            float L = 0f, R = 0f, FL = Buzz, FR = Buzz;
            // game rumble set this frame or the previous one; older means no RumbleManager in this scene
            if (GameFrame >= Time.frameCount - 1) { L = GameLeft; R = GameRight; }
            for (int i = Pulses.Count - 1; i >= 0; i--)
            {
                Pulse P = Pulses[i];
                if (P.End <= Now) { Pulses.RemoveAt(i); continue; }
                if (P.Left && P.Amplitude > L) { L = P.Amplitude; FL = P.Frequency; }
                if (P.Right && P.Amplitude > R) { R = P.Amplitude; FR = P.Frequency; }
            }
            if (!Vars.Config.Controllers.EnableHaptics) L = R = 0f;
            Send(SteamVR_Input_Sources.LeftHand, L, FL, ref SentLeft, ref SentFreqLeft);
            Send(SteamVR_Input_Sources.RightHand, R, FR, ref SentRight, ref SentFreqRight);
        }

        private static void Send(SteamVR_Input_Sources Source, float Amplitude, float Frequency, ref float Sent, ref float SentFreq)
        {
            // a zero is sent once, not every frame; a steady vibration is refreshed every frame (1 s pulses, replaced)
            if (Amplitude <= 0f && Sent == 0f) return;
            Sent = Amplitude; SentFreq = Frequency;
            Patches.Controllers.PatchControllerHaptics.Vibrate(1f, Frequency, Amplitude, Source);
        }

        // Stops both controllers now and forgets the pulses.
        public static void StopAll()
        {
            Pulses.Clear();
            GameLeft = GameRight = 0f;
            Patches.Controllers.PatchControllerHaptics.Vibrate(1f, Buzz, 0f, SteamVR_Input_Sources.LeftHand);
            Patches.Controllers.PatchControllerHaptics.Vibrate(1f, Buzz, 0f, SteamVR_Input_Sources.RightHand);
            SentLeft = SentRight = 0f;
        }
    }
}
