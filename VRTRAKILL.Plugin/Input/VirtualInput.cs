using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using WindowsInput.Native;
using WinMouseButton = WindowsInput.MouseButton;

namespace Plugin.Systems.Input
{
    // VR presses as a keyboard and a mouse that exist only inside the game's Input System. Before, every press was a
    // real Windows key press (SendInput): it went to whatever window had focus, so the game window had to stay in front
    // ("keep the ULTRAKILL window active", the most common fix on the old wiki), and Windows read key combinations
    // (Ctrl+Shift+Esc opened Task Manager, issue #117). These devices send nothing to Windows, and they keep working
    // when another window has focus. The game's bindings ("<Keyboard>/space", "<Mouse>/leftButton") match any keyboard
    // or mouse, so the player's own bindings still apply.
    internal static unsafe class VirtualInput
    {
        private static Keyboard Kb;
        private static Mouse Ms;
        private static KeyboardState KbState;
        private static MouseState MsState;
        private static readonly Dictionary<VirtualKeyCode, Key> KeyOf = new Dictionary<VirtualKeyCode, Key>();
        private static readonly HashSet<VirtualKeyCode> WarnedKeys = new HashSet<VirtualKeyCode>();

        public static bool Ready => Kb != null && Ms != null && Kb.added && Ms.added;

        // Adds the two devices; false when the Input System refuses them (the caller then uses Windows key presses).
        public static bool Init()
        {
            if (Ready) return true;
            try
            {
                // the Input System asks each device whether it may run in the background, and disables the ones that
                // may not when the game loses focus; these two may
                InputSystem.onDeviceCommand -= AnswerCommand;
                InputSystem.onDeviceCommand += AnswerCommand;
                Kb = InputSystem.AddDevice<Keyboard>("VRTRAKILL Keyboard");
                Ms = InputSystem.AddDevice<Mouse>("VRTRAKILL Mouse");
                KbState = new KeyboardState();
                MsState = new MouseState();

                // the mod's key codes come from the game's binding names (InputMap); map them back to keyboard keys
                foreach (KeyValuePair<string, VirtualKeyCode?> Pair in InputMap.Keys)
                {
                    if (Pair.Value == null || KeyOf.ContainsKey(Pair.Value.Value)) continue;
                    KeyControl Control = Kb.TryGetChildControl<KeyControl>(Pair.Key);
                    if (Control != null) KeyOf[Pair.Value.Value] = Control.keyCode;
                }
                Vars.Log.LogInfo($"VR buttons go to a virtual keyboard and mouse inside the game (background behavior {InputSystem.settings.backgroundBehavior}, "
                               + $"run in background: keyboard {Kb.canRunInBackground}, mouse {Ms.canRunInBackground}).");
                return true;
            }
            catch (Exception E)
            {
                Vars.Log.LogWarning("The virtual keyboard and mouse could not be added, so VR buttons are sent as Windows key presses: " + E);
                Remove();
                return false;
            }
        }

        private static long? AnswerCommand(InputDevice Device, InputDeviceCommand* Command)
        {
            if ((Device != Kb && Device != Ms) || Kb == null) return null;
            if (Command->type == QueryCanRunInBackground.Type)
            {
                ((QueryCanRunInBackground*)Command)->canRunInBackground = true;
                return InputDeviceCommand.GenericSuccess;
            }
            return null;
        }

        private static void Remove()
        {
            InputSystem.onDeviceCommand -= AnswerCommand;
            try { if (Kb != null && Kb.added) InputSystem.RemoveDevice(Kb); } catch { }
            try { if (Ms != null && Ms.added) InputSystem.RemoveDevice(Ms); } catch { }
            Kb = null; Ms = null;
        }

        // True when the key code is a key of the virtual keyboard.
        public static bool HasKey(VirtualKeyCode Code) => KeyOf.ContainsKey(Code);

        // Presses or releases a key. Returns false for a key the virtual keyboard does not have.
        public static bool SetKey(VirtualKeyCode Code, bool Down)
        {
            if (!Ready) return false;
            if (!KeyOf.TryGetValue(Code, out Key K))
            {
                if (WarnedKeys.Add(Code)) Vars.Log.LogWarning($"The key {Code} is not on the virtual keyboard; rebind that action in the game to another key.");
                return false;
            }
            KbState.Set(K, Down);
            InputSystem.QueueStateEvent(Kb, KbState);
            return true;
        }

        // Presses or releases a mouse button (the mod's numbering: 3 is the back button, 4 the forward button).
        public static bool SetButton(WinMouseButton Button, bool Down)
        {
            if (!Ready) return false;
            UnityEngine.InputSystem.LowLevel.MouseButton B;
            switch ((int)Button)
            {
                case (int)WinMouseButton.LeftButton: B = UnityEngine.InputSystem.LowLevel.MouseButton.Left; break;
                case (int)WinMouseButton.RightButton: B = UnityEngine.InputSystem.LowLevel.MouseButton.Right; break;
                case (int)WinMouseButton.MiddleButton: B = UnityEngine.InputSystem.LowLevel.MouseButton.Middle; break;
                case 3: B = UnityEngine.InputSystem.LowLevel.MouseButton.Back; break;
                case 4: B = UnityEngine.InputSystem.LowLevel.MouseButton.Forward; break;
                default: return false;
            }
            MsState = MsState.WithButton(B, Down);
            MsState.scroll = Vector2.zero;
            InputSystem.QueueStateEvent(Ms, MsState);
            return true;
        }

        // Scrolls the wheel by whole notches (120 each, like a Windows mouse wheel). The game reads the wheel from
        // Mouse.current (weapon switching, scrolling lists), so the virtual mouse becomes the current one.
        public static bool Scroll(int Notches)
        {
            if (!Ready) return false;
            Ms.MakeCurrent();
            MouseState S = MsState;
            S.scroll = new Vector2(0, 120f * Notches);
            InputSystem.QueueStateEvent(Ms, S);
            return true;
        }

        // Releases every key and button.
        public static void ReleaseAll()
        {
            if (!Ready) return;
            KbState = new KeyboardState();
            MsState = new MouseState();
            InputSystem.QueueStateEvent(Kb, KbState);
            InputSystem.QueueStateEvent(Ms, MsState);
        }
    }
}
