using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Plugin.Systems.Feedback
{
    // The connection to the PSVR2 Toolkit (github.com/BnuuySolutions/PSVR2Toolkit, MIT), which adds what Sony's PC
    // driver leaves out: adaptive trigger effects and eye tracking. It listens on 127.0.0.1:3364 (its "legacy IPC"):
    // an 8-byte header { uint16 type; 2 bytes padding; int32 dataLen; } then the data, little endian; a handshake, then
    // one command per send. Trigger effects are sent as they change (AdaptiveTriggers); while "Parry aim" is "Eyes", the
    // gaze is asked for about 30 times a second and kept for the parry aim (Patches/ULTRAKILL/PatchPunch), or 60 when
    // the streamer view's overlay draws it. Everything runs on its own thread, so
    // the game never waits on it. Without the Toolkit, or without PS VR2 Sense controllers, nothing connects.
    internal static class Psvr2Toolkit
    {
        public const byte Left = 0, Right = 1;
        private const int Port = 3364;
        private const ushort IpcVersion = 1; // the oldest: accepted by every Toolkit server, and its gaze answer is the shortest
        private const ushort CmdHandshake = 2, CmdHandshakeResult = 3, CmdRequestGaze = 4, CmdGazeResult = 5;
        private static volatile int GazeEveryMs = 33;

        private static Thread Worker;
        private static readonly object Gate = new object();
        // the open stream, written only under WriteLock: by the worker, and at exit by the main thread (SendNow)
        private static NetworkStream Live;
        private static readonly object WriteLock = new object();
        private static readonly Queue<byte[]> Pending = new Queue<byte[]>();
        private static volatile bool Stopping;
        public static volatile bool TriggersWanted, GazeWanted;
        public static volatile bool Connected;

        // Called once a frame on the main thread, before AdaptiveTriggers.Tick: decides what is wanted and starts the
        // connection thread the first time something is.
        public static void Tick()
        {
            bool OnPSVR2 = UnityEngine.XR.XRSettings.isDeviceActive && UI.Patches.ButtonPrompts.ControllerType() == "playstation_vr2_sense";
            TriggersWanted = OnPSVR2 && Vars.Config.Controllers.AdaptiveTriggers;
            GazeWanted = OnPSVR2 && (Vars.Config.MBP.ParryAim == "Eyes" || VRCamera.StreamerOverlay.WantsGaze);
            GazeEveryMs = VRCamera.StreamerOverlay.WantsGaze ? 16 : 33; // the overlay draws it: about 60 a second
            if ((TriggersWanted || GazeWanted) && Worker == null)
            {
                Worker = new Thread(Run) { IsBackground = true, Name = "VRTRAKILL PSVR2 Toolkit" };
                Worker.Start();
            }
        }

        public static byte[] Packet(ushort Type, params byte[] Data)
        {
            byte[] P = new byte[8 + Data.Length];
            BitConverter.GetBytes(Type).CopyTo(P, 0);
            BitConverter.GetBytes(Data.Length).CopyTo(P, 4);
            Data.CopyTo(P, 8);
            return P;
        }

        public static void Send(byte[] P)
        {
            lock (Gate) { Pending.Enqueue(P); Monitor.Pulse(Gate); }
        }

        // ---- Gaze ----
        private static readonly object GazeGate = new object();
        private static Vector3 Gaze;
        private static long GazeAt = long.MinValue;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool LoggedGaze;

        // The latest gaze direction in head space (Unity axes: x right, y up, z forward), if one came in the last
        // MaxAge seconds with at least one eye open and tracked.
        public static bool TryGetGaze(float MaxAge, out Vector3 Direction)
        {
            lock (GazeGate)
            {
                Direction = Gaze;
                return GazeAt != long.MinValue && (Clock.ElapsedTicks - GazeAt) < MaxAge * Stopwatch.Frequency;
            }
        }

        // CommandDataServerGazeDataResult_t (IPC version 1), packed: per eye, left then right, 33 bytes:
        // bool originValid; float3 originMm; bool dirValid; float3 dirNorm; bool pupilValid; float pupilMm; bool blinkValid; bool blink.
        // The Toolkit's direction has x pointing left: Unity's is (-x, y, z), as in the Toolkit's other clients.
        private static void ReadGaze(byte[] D)
        {
            Vector3 Sum = Vector3.zero;
            int Eyes = 0;
            for (int Eye = 0; Eye < 2; Eye++)
            {
                int O = Eye * 33;
                if (D[O + 13] == 0 || (D[O + 31] != 0 && D[O + 32] != 0)) continue; // no direction, or blinking
                Vector3 V = new Vector3(-BitConverter.ToSingle(D, O + 14), BitConverter.ToSingle(D, O + 18), BitConverter.ToSingle(D, O + 22));
                if (V.sqrMagnitude < .5f || V.z <= 0f) continue;
                Sum += V.normalized; Eyes++;
            }
            if (Eyes == 0) return;
            Vector3 Dir = (Sum / Eyes).normalized;
            lock (GazeGate) { Gaze = Dir; GazeAt = Clock.ElapsedTicks; }
            if (!LoggedGaze)
            {
                LoggedGaze = true;
                Vars.Log.LogInfo($"Eye tracking: gaze received from the PSVR2 Toolkit ({Mathf.Atan2(Dir.x, Dir.z) * Mathf.Rad2Deg:0}° right, {Mathf.Asin(Dir.y) * Mathf.Rad2Deg:0}° up of the headset's forward).");
            }
        }

        // ---- Connection ----
        private static void Run()
        {
            bool LoggedMissing = false;
            byte[] GazeRequest = Packet(CmdRequestGaze);
            while (!Stopping)
            {
                if (!TriggersWanted && !GazeWanted) { Thread.Sleep(1000); continue; }
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
                        BitConverter.GetBytes((uint)Process.GetCurrentProcess().Id).CopyTo(Hello, 4);
                        byte[] H = Packet(CmdHandshake, Hello);
                        Stream.Write(H, 0, H.Length);
                        byte[] Answer = ReadExactly(Stream, 12); // header + { uint8 result; (1 byte padding) uint16 ipcVersion; }
                        if (BitConverter.ToUInt16(Answer, 0) != CmdHandshakeResult || Answer[8] != 1)
                            throw new IOException($"handshake refused (result {Answer[8]}, server IPC version {BitConverter.ToUInt16(Answer, 10)})");
                        lock (Gate) Pending.Clear();
                        lock (WriteLock) Live = Stream;
                        Connected = true; LoggedMissing = false;
                        Vars.Log.LogInfo($"PSVR2 Toolkit: connected (IPC version {BitConverter.ToUInt16(Answer, 10)}); adaptive triggers {(TriggersWanted ? "on" : "off")}, eye tracking {(GazeWanted ? "asked for" : "off")}.");

                        long NextGaze = 0;
                        while (!Stopping)
                        {
                            byte[] Next = null;
                            lock (Gate)
                            {
                                if (Pending.Count == 0) Monitor.Wait(Gate, GazeWanted ? GazeEveryMs : 1000);
                                if (Pending.Count > 0) Next = Pending.Dequeue();
                            }
                            if (Next != null)
                            {
                                lock (WriteLock) Stream.Write(Next, 0, Next.Length);
                                Thread.Sleep(15); // the server reads one command per receive: keep sends apart
                            }
                            if (GazeWanted && Clock.ElapsedMilliseconds >= NextGaze)
                            {
                                NextGaze = Clock.ElapsedMilliseconds + GazeEveryMs;
                                byte[] Head, Data;
                                lock (WriteLock)
                                {
                                    Stream.Write(GazeRequest, 0, GazeRequest.Length);
                                    Head = ReadExactly(Stream, 8); // the answer comes before the next send, so sends stay apart
                                    int Length = BitConverter.ToInt32(Head, 4);
                                    if (Length < 0 || Length > 1024) throw new IOException("unexpected answer length " + Length);
                                    Data = ReadExactly(Stream, Length);
                                }
                                if (BitConverter.ToUInt16(Head, 0) == CmdGazeResult && Data.Length >= 66) ReadGaze(Data);
                            }
                            else if (Next == null && !TriggersWanted && !GazeWanted) break;
                        }
                    }
                }
                catch (Exception E)
                {
                    if (Connected) Vars.Log.LogInfo("PSVR2 Toolkit: connection lost (" + E.Message + ").");
                    else if (!LoggedMissing) { LoggedMissing = true; Vars.Log.LogInfo("PSVR2 Toolkit: not found (" + E.Message + "); trying again every few seconds."); }
                }
                lock (WriteLock) Live = null;
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

        // Writes commands at once from the calling thread, spaced like the worker's, and returns whether they went out.
        // For exit: queued commands go out on the worker thread, which the process may not wait for.
        public static bool SendNow(params byte[][] Packets)
        {
            lock (WriteLock)
            {
                if (Live == null) return false;
                try
                {
                    foreach (byte[] P in Packets) { Live.Write(P, 0, P.Length); Live.Flush(); Thread.Sleep(15); }
                    return true;
                }
                catch (Exception) { return false; }
            }
        }

        // At exit: stops the worker; the caller frees what it set first (AdaptiveTriggers.Shutdown).
        public static void Shutdown()
        {
            Stopping = true;
            lock (Gate) Monitor.Pulse(Gate);
        }
    }
}
