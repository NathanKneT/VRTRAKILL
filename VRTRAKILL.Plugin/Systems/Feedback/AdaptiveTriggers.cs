using UnityEngine;

namespace Plugin.Systems.Feedback
{
    // Adaptive trigger effects on PS VR2 Sense controllers, through the PSVR2 Toolkit (see Psvr2Toolkit). Sony's PC
    // driver leaves the adaptive triggers unused. The gun hand's trigger gets an effect per weapon (a crisp break on the revolver, a
    // heavy one on the shotgun, a rattle on the nailgun, a hum on the charged railcannon...), and the arm hand's a
    // light click for the Feedbacker and a firm push for the Knuckleblaster. The gun trigger also follows the weapon's
    // state: the break grows heavier while a charge builds (Piercer, shotgun pump and core, cannonball, jackhammer
    // swing), the trigger hums once the charge is full or the railcannon is ready, and it locks stiff while the
    // railcannon recharges or the nailgun has burnt out. Menus, the shop and pause free both triggers, so clicking stays
    // light. Without the Toolkit, or with other controllers, nothing is sent.
    // "Adaptive triggers" in the controller settings (TRIGGERS in the VR column) turns it off.
    internal static class AdaptiveTriggers
    {
        private const ushort CmdOff = 6, CmdFeedback = 7, CmdWeapon = 8, CmdVibration = 9;
        private const byte Left = Psvr2Toolkit.Left, Right = Psvr2Toolkit.Right;

        private static readonly string[] Sent = new string[2];
        private static GameObject CachedWeapon; private static string CachedWeaponKind; private static Component CachedComponent;

        // Called once a frame on the main thread, after Psvr2Toolkit.Tick: works out each trigger's effect and queues the
        // ones that changed.
        public static void Tick()
        {
            if (!Psvr2Toolkit.TriggersWanted)
            {
                if (Psvr2Toolkit.Connected && (Sent[0] != null || Sent[1] != null)) { Psvr2Toolkit.Send(Off(Left)); Psvr2Toolkit.Send(Off(Right)); }
                Sent[0] = Sent[1] = null;
                return;
            }
            if (!Psvr2Toolkit.Connected) { Sent[0] = Sent[1] = null; return; }

            bool LeftHanded = Vars.Config.Controllers.LeftHanded;
            byte GunHand = LeftHanded ? Left : Right, ArmHand = LeftHanded ? Right : Left;
            bool Free = Vars.IsMainMenu || Vars.IsPaused || Vars.IsPlayerFrozen || Vars.IsPlayerUsingShop;
            Apply(GunHand, Free ? ("off", Off(GunHand)) : GunEffect(GunHand));
            Apply(ArmHand, Free ? ("off", Off(ArmHand)) : ArmEffect(ArmHand));
        }

        private static void Apply(byte Hand, (string Key, byte[] Packet) Effect)
        {
            if (Sent[Hand] == Effect.Key) return;
            Sent[Hand] = Effect.Key;
            Psvr2Toolkit.Send(Effect.Packet);
        }

        // Positions run 0 (released) to 9 (fully pulled), a tenth of the travel each; strengths 1 to 8 (Sony's ScePad
        // trigger effect ranges). The PS VR2 bindings fire at 75 % of the travel, so every break ends by position 5:
        // the shot leaves as the trigger gives way, not halfway through the resistance ("it fires before I get to the
        // end", PS VR2 test).
        private static (string, byte[]) GunEffect(byte Hand)
        {
            GunControl GC = MonoSingleton<GunControl>.Instance;
            GameObject W = GC != null ? GC.currentWeapon : null;
            if (W != CachedWeapon) { CachedWeapon = W; CachedWeaponKind = Kind(W, out CachedComponent); }
            if (CachedComponent == null) return ("off", Off(Hand));
            switch (CachedWeaponKind)
            {
                case "revolver":
                    Revolver R = (Revolver)CachedComponent;
                    if (R.pierceShotCharge >= 100f) return ("revolver ready", Ready(Hand));
                    if (R.pierceShotCharge > 0f) return Charging("revolver", Hand, R.pierceShotCharge / 100f, 3, 5, 5);
                    return ("revolver", Weapon(Hand, 3, 5, 5));                     // a crisp break, like a trigger
                case "shotgun":
                    Shotgun S = (Shotgun)CachedComponent;
                    if (S.variation == 1 && S.primaryCharge >= 3) return ("shotgun overpumped", Vibration(Hand, 1, 7, 50)); // one more pump explodes
                    if (S.variation == 1 && S.primaryCharge > 0) return Charging("shotgun", Hand, S.primaryCharge / 3f, 2, 5, 6);
                    if (S.charging) return Charging("shotgun", Hand, S.grenadeForce / 60f, 2, 5, 6);
                    return ("shotgun", Weapon(Hand, 2, 5, 6));                      // a long, heavy break
                case "jackhammer":
                    ShotgunHammer H = (ShotgunHammer)CachedComponent;
                    if (H.chargingSwing && H.swingCharge >= 1f) return ("jackhammer ready", Ready(Hand));
                    if (H.chargingSwing) { int L = Level(H.swingCharge); return ("jackhammer " + L, Feedback(Hand, 2, (byte)Mathf.Min(8, 5 + L))); }
                    return ("jackhammer", Feedback(Hand, 2, 6));                    // firm all the way
                case "nailgun":
                    if (((Nailgun)CachedComponent).burnOut) return ("nailgun burnt out", Locked(Hand));
                    return ("nailgun", Vibration(Hand, 7, 4, 40));                  // a rattle once it fires
                case "railcannon":
                    WeaponCharges Charges = MonoSingleton<WeaponCharges>.Instance;
                    if (Charges != null && Charges.raicharge < 5f && !ULTRAKILL.Cheats.NoWeaponCooldown.NoCooldown) return ("railcannon charging", Locked(Hand));
                    return ("railcannon", Ready(Hand));
                case "rocket":
                    RocketLauncher RL = (RocketLauncher)CachedComponent;
                    if (RL.cbCharge >= 1f) return ("rocket ready", Ready(Hand));
                    if (RL.cbCharge > 0f) return Charging("rocket", Hand, RL.cbCharge, 4, 5, 6);
                    return ("rocket", Weapon(Hand, 4, 5, 6));                       // a late, firm break
                default: return ("off", Off(Hand));
            }
        }

        // A charge in four steps, each a heavier break than the last, up to the strongest (8).
        private static (string, byte[]) Charging(string Name, byte Hand, float Charge, byte Start, byte End, byte Strength)
        {
            int L = Level(Charge);
            return (Name + " " + L, Weapon(Hand, Start, End, (byte)Mathf.Min(8, Strength + L)));
        }
        private static int Level(float Charge) => Mathf.Clamp(Mathf.CeilToInt(Charge * 4f), 1, 4);
        // a hum from the first tenth of the pull: charged, fire when you like
        private static byte[] Ready(byte Hand) => Vibration(Hand, 1, 3, 30);
        // a stiff wall from the first tenth: this weapon cannot fire yet
        private static byte[] Locked(byte Hand) => Feedback(Hand, 1, 8);

        private static (string, byte[]) ArmEffect(byte Hand)
        {
            FistControl Fists = MonoSingleton<FistControl>.Instance;
            Punch P = Fists != null ? Fists.currentPunch : null;
            if (P == null) return ("off", Off(Hand));
            if (P.type == FistType.Heavy) return ("knuckleblaster", Feedback(Hand, 3, 6));
            return ("feedbacker", Weapon(Hand, 3, 5, 3));
        }

        private static string Kind(GameObject W, out Component C)
        {
            C = null;
            if (W == null) return null;
            if ((C = W.GetComponent<Revolver>()) != null) return "revolver";
            if ((C = W.GetComponent<ShotgunHammer>()) != null) return "jackhammer";
            if ((C = W.GetComponent<Shotgun>()) != null) return "shotgun";
            if ((C = W.GetComponent<Nailgun>()) != null) return "nailgun";
            if ((C = W.GetComponent<Railcannon>()) != null) return "railcannon";
            if ((C = W.GetComponent<RocketLauncher>()) != null) return "rocket";
            return null;
        }

        private static byte[] Off(byte Hand) => Psvr2Toolkit.Packet(CmdOff, Hand);
        private static byte[] Feedback(byte Hand, byte Position, byte Strength) => Psvr2Toolkit.Packet(CmdFeedback, Hand, Position, Strength);
        private static byte[] Weapon(byte Hand, byte Start, byte End, byte Strength) => Psvr2Toolkit.Packet(CmdWeapon, Hand, Start, End, Strength);
        private static byte[] Vibration(byte Hand, byte Position, byte Amplitude, byte Frequency) => Psvr2Toolkit.Packet(CmdVibration, Hand, Position, Amplitude, Frequency);

        // At exit: free both triggers, so they are not left stiff for the next game.
        public static void Shutdown()
        {
            if (Psvr2Toolkit.Connected) { Psvr2Toolkit.Send(Off(Left)); Psvr2Toolkit.Send(Off(Right)); }
            Psvr2Toolkit.Shutdown();
        }
    }
}
