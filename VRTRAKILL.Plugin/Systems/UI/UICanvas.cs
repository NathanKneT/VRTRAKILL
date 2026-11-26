using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plugin.Systems.UI
{
    // adapted from HuskVR
    internal sealed class UICanvas : MonoBehaviour
    {
        private Vector3 LastCamFwd = Vector3.zero;
        private int LastChildren;

        private const float Distance = 72f;
        // Returns the UI size from the config.
        private static float Scale => Vars.Config.UIInteraction.UISize;

        // Stores the gaze direction at the UI distance and turns the canvas with the camera.
        private void UpdatePos()
        {
            LastCamFwd = Vars.MainCamera.transform.forward * Distance;
            transform.rotation = Vars.MainCamera.transform.rotation;
        }
        // Flattens the stored direction against gravity and turns the canvas upright along it.
        private void ResetPos()
        {
            Vector3 customUp = Vars.Registered<CameraController>() is CameraController CC ? CC.gravityRotation * Vector3.up : Vector3.up;

            LastCamFwd = Vector3.ProjectOnPlane(LastCamFwd, customUp);
            if (LastCamFwd != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(LastCamFwd, customUp);
        }
        // Re-takes the gaze yaw at full distance; a near-vertical gaze takes its heading from the head's up axis.
        private void Recenter()
        {
            UpdatePos();
            Vector3 customUp = Vars.Registered<CameraController>() is CameraController CC ? CC.gravityRotation * Vector3.up : Vector3.up;
            Vector3 Flat = Vector3.ProjectOnPlane(LastCamFwd, customUp);
            if (Flat.sqrMagnitude < Distance * Distance * .0625f)
                Flat = Vector3.ProjectOnPlane(Vars.MainCamera.transform.up * (Vector3.Dot(LastCamFwd, customUp) < 0 ? 1 : -1), customUp);
            if (Flat != Vector3.zero) LastCamFwd = Flat.normalized * Distance;
        }

        // Returns a hash of the active direct children, which changes when the menu changes page.
        private int ActiveChildren()
        {
            int Sig = 17;
            // an index loop; foreach over a Transform allocates an enumerator
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform Child = transform.GetChild(i);
                if (Child.gameObject.activeSelf) Sig = Sig * 31 + Child.GetInstanceID();
            }
            return Sig;
        }

        // Scales the canvas, sizes a root scaled canvas to its reference resolution and places it in front of the view.
        public void Start()
        {
            transform.localScale = Vector3.one * Scale;
            // the converted rect keeps the monitor's aspect, which pushes edge-anchored panels off to the sides
            if (GetComponent<Canvas>() is Canvas C && C.isRootCanvas
                && GetComponent<CanvasScaler>() is CanvasScaler CS && CS.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                RectTransform RT = (RectTransform)transform;
                RT.sizeDelta = CS.referenceResolution;
                RT.pivot = new Vector2(.5f, .5f); // keeps the rect centred on the gaze point
            }
            LastCamFwd = Vector3.back * Distance;
            UpdatePos();
            LastChildren = ActiveChildren();
            RememberActive();
        }
        // A frozen menu moved to the view each time its page changed, which it reads from its direct children turning on or
        // off. Anything else that turns on or off there (a tooltip, a hint, a subtitle, another mod's panel, a part that
        // blinks) moved it too, and always so that the point looked at became its middle: looking at the right side of a
        // menu while that happened slid the menu further right each time (issue #1, "right side of menu started
        // drifting"; not confirmed in a headset). It now moves only when the player looks away from it, past its side
        // edge plus a margin: a page that opens while you look at the menu stays where you see it, a page that opens while
        // you look elsewhere still comes to you, whatever changes in between and however often. The first change after the
        // menu was following the view (a pause, the rank screen, a new scene) still moves it as before: that sets it at
        // its full distance on the horizon even when the head was tilted.
        private const float LookAwayMargin = 10f; // degrees past the menu's side edge
        private const float MaxEdge = 30f;        // a canvas left at a monitor's size would otherwise count as very wide
        private bool RecenterNext = true;
        private const int MaxChangeLogs = 40;      // per game: enough to see what keeps changing in a bug report
        private static int ChangesLogged;

        // How far the gaze is from the menu's middle, around the player's up, and how far the menu's side edge is.
        private bool LookingAway(out float Off, out float Edge)
        {
            Vector3 Up = Vars.Registered<CameraController>() is CameraController CC ? CC.gravityRotation * Vector3.up : Vector3.up;
            Vector3 Gaze = Vector3.ProjectOnPlane(Vars.MainCamera.transform.forward, Up), Menu = Vector3.ProjectOnPlane(LastCamFwd, Up);
            RectTransform RT = transform as RectTransform;
            float Half = RT != null ? RT.rect.width * Mathf.Abs(transform.lossyScale.x) * .5f : 0f;
            Edge = Half > .01f && !float.IsNaN(Half) && !float.IsInfinity(Half) ? Mathf.Min(Mathf.Atan2(Half, Distance) * Mathf.Rad2Deg, MaxEdge) : MaxEdge;
            // straight up or down there is no heading to compare: treat it as looking away, as before
            Off = Gaze.sqrMagnitude < 1e-4f || Menu.sqrMagnitude < 1e-4f ? 180f : Vector3.Angle(Gaze, Menu);
            return Off > Edge + LookAwayMargin;
        }

        // The direct children that turned on or off since the last check, for the log (only while it still logs).
        private readonly HashSet<int> WasActive = new HashSet<int>();
        private void RememberActive()
        {
            WasActive.Clear();
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).gameObject.activeSelf) WasActive.Add(transform.GetChild(i).GetInstanceID());
        }
        private string ChangedChildren()
        {
            List<string> On = new List<string>(), Off = new List<string>();
            HashSet<int> Now = new HashSet<int>(), Children = new HashSet<int>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform Child = transform.GetChild(i);
                int Id = Child.GetInstanceID();
                bool Active = Child.gameObject.activeSelf;
                Children.Add(Id);
                if (Active) Now.Add(Id);
                if (Active != WasActive.Contains(Id)) (Active ? On : Off).Add(Child.name);
            }
            int Gone = 0;
            foreach (int Id in WasActive) if (!Children.Contains(Id)) Gone++;
            WasActive.Clear(); WasActive.UnionWith(Now);
            return $"on: {Names(On)}; off: {Names(Off)}{(Gone > 0 ? $"; {Gone} removed" : "")}";
        }
        private static string Names(List<string> L)
            => L.Count == 0 ? "-" : string.Join(", ", L.Count > 6 ? L.GetRange(0, 6) : L) + (L.Count > 6 ? $" and {L.Count - 6} more" : "");

        // Keeps the canvas in front of the view; while frozen, holds it in place unless the menu page changes while the
        // player looks away from it.
        public void LateUpdate()
        {
            int Children = ActiveChildren();
            bool Logging = ChangesLogged < MaxChangeLogs;
            if (!Vars.IsPlayerFrozen)
            {
                UpdatePos();
                RecenterNext = true;
                if (Children != LastChildren && Logging) RememberActive();
            }
            else
            {
                if (Children != LastChildren)
                {
                    bool Away = LookingAway(out float Off, out float Edge), First = RecenterNext;
                    RecenterNext = false;
                    if (Away || First) Recenter();
                    if (Logging)
                    {
                        ChangesLogged++;
                        Vars.Log.LogInfo($"Menu \"{name}\" changed ({ChangedChildren()}); the gaze is {Off:0} deg from its middle, its edge at {Edge:0} deg: "
                                       + (First ? "first change, moved to the view." : Away ? "moved to the view." : "stays where it is."));
                    }
                }
                ResetPos();
            }
            LastChildren = Children;
            transform.position = Vars.MainCamera.transform.position + LastCamFwd;
            // "UI SIZE" in the VR settings applies at once
            if (transform.localScale.x != Scale) transform.localScale = Vector3.one * Scale;
        }
    }
}
