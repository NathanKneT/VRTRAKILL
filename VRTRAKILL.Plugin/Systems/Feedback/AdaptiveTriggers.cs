using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Plugin.Systems.Feedback
{
    // Adaptive trigger effects on PS VR2 Sense controllers, through the PSVR2 Toolkit. Sony's PC driver leaves the
    // adaptive triggers unused; the open source PSVR2 Toolkit (github.com/BnuuySolutions/PSVR2Toolkit, MIT) adds them and
    // takes effect commands over a local TCP connection on port 3364 (its "legacy IPC": an 8-byte header, a handshake,
    // then one command per send). The gun hand's trigger gets an effect per weapon (a crisp break on the revolver, a
    // heavy one on the shotgun, a rattle on the nailgun, a hum on the charged railcannon...), and the arm hand's a
    // light click for the Feedbacker and a firm push for the Knuckleblaster. The gun trigger also follows the weapon's
    // state: the break grows heavier while a charge builds (Piercer, shotgun pump and core, cannonball, jackhammer
    // swing), the trigger hums once the charge is full or the railcannon is ready, and it locks stiff while the
    // railcannon recharges or the nailgun has burnt out. Menus, the shop and pause free both triggers, so clicking stays
    // light. Without the Toolkit, or with other controllers, nothing is sent.
    // "Adaptive triggers" in the controller settings (TRIGGERS in the VR column) turns it off.
    internal static class AdaptiveTriggers
    {
        private const int Port = 3364;
        private const ushort IpcVersion = 1; // the oldest: accepted by every Toolkit server; it only changes gaze data
        private const ushort CmdHandshake = 2, CmdHandshakeResult = 3, CmdOff = 6, CmdFeedback = 7, CmdWeapon = 8, CmdVibration = 9;
        private const byte Left = 0, Right = 1;

        private static Thread Worker;
        private static readonly object Gate = new object();
        private static readonly Queue<byte[]> Pending = new Queue<byte[]>();
        private static volatile bool Wanted, Connected, Stopping;
        private static readonly string[] Sent = new string[2];
        private static GameObject CachedWeapon; private static string CachedWeaponKind; private static Component CachedComponent;

        // Called once a frame on the main thread: works out each trigger's effect and queues the ones that changed.
        public static void Tick()
        {
            bool Want = Vars.Config.Controllers.AdaptiveTriggers && UnityEngine.XR.XRSettings.isDeviceActive
                        && UI.Patches.ButtonPrompts.ControllerType() == "playstation_vr2_sense";
            if (!Want)
            {
                if (Connected && (Sent[0] != null || Sent[1] != null)) { Queue(Off(Left)); Queue(Off(Right)); }
                Sent[0] = Sent[1] = null;
                Wanted = false;
                return;
            }
            Wanted = true;
            if (Worker == null) { Worker = new Thread(Run) { IsBackground = true, Name = "VRTRAKILL adaptive triggers" }; Worker.Start(); }
            if (!Connected) { Sent[0] = Sent[1] = null; return; }

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
            Queue(Effect.Packet);
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

        // ---- Packets: CommandHeader_t { uint16 type; (2 bytes padding) int32 dataLen; } then the data, little endian ----
        private static byte[] Packet(ushort Type, params byte[] Data)
        {
            byte[] P = new byte[8 + Data.Length];
            BitConverter.GetBytes(Type).CopyTo(P, 0);
            BitConverter.GetBytes(Data.Length).CopyTo(P, 4);
            Data.CopyTo(P, 8);
            return P;
        }
        private static byte[] Off(byte Hand) => Packet(CmdOff, Hand);
        private static byte[] Feedback(byte Hand, byte Position, byte Strength) => Packet(CmdFeedback, Hand, Position, Strength);
        private static byte[] Weapon(byte Hand, byte Start, byte End, byte Strength) => Packet(CmdWeapon, Hand, Start, End, Strength);
        private static byte[] Vibration(byte Hand, byte Position, byte Amplitude, byte Frequency) => Packet(CmdVibration, Hand, Position, Amplitude, Frequency);

        private static void Queue(byte[] P)
        {
            lock (Gate) { Pending.Enqueue(P); Monitor.Pulse(Gate); }
        }

        // ---- Connection, on its own thread so the game never waits on it ----
        private static void Run()
        {
            bool LoggedMissing = false;
            while (!Stopping)
            {
                if (!Wanted) { Thread.Sleep(1000); continue; }
                try
                {
                    using (TcpClient Client = new TcpClient())
                    {
                        Client.NoDelay = true;
                        IAsyncResult Attempt = Client.BeginConnect("127.0.0.1", Port, null, null);
                        if (!Attempt.AsyncWaitHandle.WaitOne(1000)) throw new IOException("no answer on port " + Port);
                        Client.EndConnect(Attempt);
                        NetworkStream Stream = Client.GetStream();
                        Stream.ReadTimeout = 2000;

                        byte[] Hello = new byte[8];
                        BitConverter.GetBytes(IpcVersion).CopyTo(Hello, 0);
                        BitConverter.GetBytes((uint)System.Diagnostics.Process.GetCurrentProcess().Id).CopyTo(Hello, 4);
                        byte[] H = Packet(CmdHandshake, Hello);
                        Stream.Write(H, 0, H.Length);
                        byte[] Answer = ReadExactly(Stream, 12); // header + { uint8 result; (1 byte padding) uint16 ipcVersion; }
                        if (BitConverter.ToUInt16(Answer, 0) != CmdHandshakeResult || Answer[8] != 1)
                            throw new IOException($"handshake refused (result {Answer[8]}, server IPC version {BitConverter.ToUInt16(Answer, 10)})");
                        lock (Gate) Pending.Clear();
                        Connected = true; LoggedMissing = false;
                        Vars.Log.LogInfo($"Adaptive triggers: connected to the PSVR2 Toolkit (IPC version {BitConverter.ToUInt16(Answer, 10)}).");

                        while (!Stopping)
                        {
                            byte[] Next = null;
                            lock (Gate)
                            {
                                if (Pending.Count == 0) Monitor.Wait(Gate, 1000);
                                if (Pending.Count > 0) Next = Pending.Dequeue();
                            }
                            if (Next == null) { if (!Wanted) break; continue; }
                            Stream.Write(Next, 0, Next.Length);
                            Thread.Sleep(15); // the server reads one command per receive: keep sends apart
                        }
                    }
                }
                catch (Exception E)
                {
                    if (Connected) Vars.Log.LogInfo("Adaptive triggers: connection to the PSVR2 Toolkit lost (" + E.Message + ").");
                    else if (!LoggedMissing) { LoggedMissing = true; Vars.Log.LogInfo("Adaptive triggers: no PSVR2 Toolkit found (" + E.Message + "); trying again every few seconds."); }
                }
                Connected = false;
                if (!Stopping) Thread.Sleep(5000);
            }
        }

        private static byte[] ReadExactly(NetworkStream Stream, int Count)
        {
            byte[] Buffer = new byte[Count];
            int Got = 0;
            while (Got < Count)
            {
                int N = Stream.Read(Buffer, Got, Count - Got);
                if (N <= 0) throw new IOException("closed by the Toolkit");
                Got += N;
            }
            return Buffer;
        }

        // At exit: free both triggers, so they are not left stiff for the next game.
        public static void Shutdown()
        {
            if (Connected) { Queue(Off(Left)); Queue(Off(Right)); }
            for (int i = 0; i < 20 && Connected; i++)
            {
                lock (Gate) { if (Pending.Count == 0) break; }
                Thread.Sleep(10);
            }
            if (Connected) Thread.Sleep(40); // the last command may still be on its way out
            Stopping = true;
            lock (Gate) Monitor.Pulse(Gate);
        }
    }
}
