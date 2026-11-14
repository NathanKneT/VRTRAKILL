namespace Plugin.Prefs
{
    // Three comfort presets for the VR column, so a new player does not have to know which of five settings help with
    // motion sickness. A preset only sets existing settings; the column shows the preset the current settings match,
    // or CUSTOM once one of them is changed by hand. Movement and jump speed are never part of a preset: lower values can
    // make level jumps impossible.
    //   BEGINNER   snap turning 45 deg, comfort vignette on (0.95), no screen flashes, still weapons
    //   STANDARD   the defaults: smooth turning 300 deg/s, no vignette, reduced flashes, a tenth of the weapon shake
    //   ULTRAKILL  smooth turning 360 deg/s, no vignette, full flashes, the game's weapon shake
    internal static class ComfortPresets
    {
        public static readonly string[] Names = { "BEGINNER", "STANDARD", "ULTRAKILL" };

        public static void Apply(string Name)
        {
            NewConfig C = Systems.Vars.Config;
            switch (Name)
            {
                case "BEGINNER":
                    C.Controllers.SnapTurn = true; C.Controllers.SnapAngles = 45f;
                    C.Comfort.Vignette = true; C.Comfort.VignetteStrength = .95f;
                    C.Comfort.ScreenFlashes = "Off"; C.Comfort.WeaponShake = 0f;
                    break;
                case "STANDARD":
                    C.Controllers.SnapTurn = false; C.Controllers.SmoothSpeed = 300f;
                    C.Comfort.Vignette = false; C.Comfort.VignetteStrength = .85f;
                    C.Comfort.ScreenFlashes = "Reduced"; C.Comfort.WeaponShake = .1f;
                    break;
                case "ULTRAKILL":
                    C.Controllers.SnapTurn = false; C.Controllers.SmoothSpeed = 360f;
                    C.Comfort.Vignette = false; C.Comfort.VignetteStrength = .85f;
                    C.Comfort.ScreenFlashes = "Full"; C.Comfort.WeaponShake = 1f;
                    break;
            }
        }

        // The preset the current settings match, or "CUSTOM". Turn speed only counts for smooth turning, snap angle
        // only for snap turning, and vignette strength only with the vignette on.
        public static string Current()
        {
            NewConfig C = Systems.Vars.Config;
            var T = C.Controllers; var K = C.Comfort;
            if (T.SnapTurn && Near(T.SnapAngles, 45f) && K.Vignette && Near(K.VignetteStrength, .95f) && K.ScreenFlashes == "Off" && Near(K.WeaponShake, 0f))
                return "BEGINNER";
            if (!T.SnapTurn && Near(T.SmoothSpeed, 300f) && !K.Vignette && K.ScreenFlashes == "Reduced" && Near(K.WeaponShake, .1f))
                return "STANDARD";
            if (!T.SnapTurn && Near(T.SmoothSpeed, 360f) && !K.Vignette && K.ScreenFlashes == "Full" && Near(K.WeaponShake, 1f))
                return "ULTRAKILL";
            return "CUSTOM";
        }

        // The next preset after the current one; from CUSTOM, the gentlest.
        public static string Next()
        {
            int I = System.Array.IndexOf(Names, Current());
            return Names[(I + 1) % Names.Length];
        }

        private static bool Near(float A, float B) => System.Math.Abs(A - B) < .001f;
    }
}
