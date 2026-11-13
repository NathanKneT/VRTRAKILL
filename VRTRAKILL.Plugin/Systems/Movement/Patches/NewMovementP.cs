using HarmonyLib;

namespace Plugin.Systems.Movement.Patches
{
    [HarmonyPatch(typeof(NewMovement))]
    internal sealed class NewMovementP
    {
        // Scales walk speed by the movement multiplier, and jump and wall jump power by the jump multiplier, before the player starts.
        // Both default to 1 (the game's values); jump power is separate because lowering it makes level jumps impossible.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(NewMovement.Start))]
        static void Start(NewMovement __instance)
        {
            __instance.walkSpeed *= Vars.Config.MovementMultiplier;
            __instance.jumpPower *= Vars.Config.JumpMultiplier;
            __instance.wallJumpPower *= Vars.Config.JumpMultiplier;
        }
    }
}
