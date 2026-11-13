using UnityEngine;

namespace Plugin.Systems.UI
{
    // Moves the crosshair onto the shot line, or ahead of the view in menus.
    internal class CrosshairController : MonoBehaviour
    {
        // Distance between the object and the crosshair
        public float Length => Vars.Config.CBS.CrosshairDistance;

        // A magic number which gives us the most accurate center
        Vector3 Offset = new Vector3(-.2f, -2.75f, 0);

        // Places the crosshair ahead of the view while frozen without controller UI, else on the shot line.
        // Every gun fires from the gun hand along its aim: CameraControllerTranspiler and PatchRocketLauncher send the
        // shot code's camera reads to Vars.DominantHand. The crosshair used to be placed from GunControl's transform, or
        // from the weapon model for the rocket launcher and jackhammer, which sit apart from the hand: measured in Level
        // 1-1, it was 3 degrees off the shot line for every gun and 5.6 for the rocket launcher, while the rocket itself
        // left 0.00 degrees off the hand's aim (issues #165, #195). Placing it from the gun hand puts it on the line.
        public void LateUpdate()
        {
            if ((Vars.IsPlayerFrozen || Vars.IsPlayerUsingShop) && !Vars.Config.UIInteraction.ControllerBased)
                transform.position = Vars.MainCamera.transform.position + (Vars.MainCamera.transform.forward * Length * .25f) + Offset;
            else
            {
                Transform Target = Vars.DominantHand.transform;
                transform.position = Target.position + (Target.forward * Length);
                transform.rotation = Target.rotation;
            }
        }
    }
}
