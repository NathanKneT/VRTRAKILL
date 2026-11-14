using HarmonyLib;

namespace Plugin.Systems.UI.Patches
{
    // The game's hints say which button punches ("Press L2 to PUNCH", the button named by ButtonPrompts). With
    // "Enable movement-based punching" the button alone does nothing: you hold it and swing. The PS VR2 tester
    // pressed it and waited ("I can't swing, it doesn't work"). A punch hint now adds a VR line: hold it and swing your
    // arm. The game's own text stays as it is. (1.x had a full list of VR texts in Input/ControlMessages, which the
    // 22957324 port stopped applying; it replaced whole messages by keyword and broke on the TMP hints.)
    [HarmonyPatch(typeof(HudMessage), nameof(HudMessage.PlayMessage))]
    internal static class TutorialTextP
    {
        private const string SwingLine = "\n<size=80%>VR: hold it and swing your arm</size>";

        static void Prefix(HudMessage __instance, out string __state)
        {
            __state = null;
            if (!UnityEngine.XR.XRSettings.isDeviceActive || !Vars.Config.EnableMBP || __instance.advancedMessage) return;
            string Action = __instance.actionReference?.action?.name;
            if (Action == null || !Action.Replace(" ", "").StartsWith("Punch", System.StringComparison.OrdinalIgnoreCase)) return;
            __state = __instance.message2 ?? "";
            __instance.message2 = (__instance.message2 ?? "") + SwingLine;
        }

        // the HudMessage can play again (notOneTime), and the setting can change: put the game's text back
        static void Postfix(HudMessage __instance, string __state)
        {
            if (__state != null) __instance.message2 = __state;
        }
    }
}
