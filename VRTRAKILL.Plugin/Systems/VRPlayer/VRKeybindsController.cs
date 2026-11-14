using UnityEngine;

namespace Plugin.Systems.VRPlayer
{
    public class VRKeybindsController : MonoSingleton<VRKeybindsController>
    {
        // Does nothing.
        public void Start() { }

        // Recalibrates the height or toggles the avatar size adjustment when their keys are pressed (the streamer view
        // key is read by StreamerView, in every scene).
        public void Update()
        {
            if (UnityEngine.Input.GetKeyDown((KeyCode)Prefs.ConfigMaster.RecalibrateHeight))
            {
                VRCamera.Patches.CameraConverterP.RequestHeightCalibration();
                SubtitleController.Instance.DisplaySubtitle("VR: height recalibrated");
            }
            if (UnityEngine.Input.GetKeyDown((KeyCode)Prefs.ConfigMaster.ToggleAvatarSizeAdj))
            {
                // (bool) of a null bool? threw InvalidOperationException, which the NullReferenceException catch missed
                if (VRAvatar.AvatarSizeAdjustor.Instance is VRAvatar.AvatarSizeAdjustor Adjustor && Adjustor != null)
                    Adjustor.enabled = !Adjustor.enabled;
                else SubtitleController.Instance.DisplaySubtitle("Unable to toggle avatar size adjustment!");
            }
        }

    }
}
