using UnityEngine;
using Plugin.Systems.VRAvatar.Armature;
using ULTRAKILL.Portal;

namespace Plugin.Systems.Arms;

internal class VRArmTransformer : MonoBehaviour
{
    public Arm Arm;
    Vector3 ArmSize, HandSize;
    // the fist and the whiplash, which the arm hand holds; the revolver, fishing rod and sandbox arm are in the gun hand
    bool OnArmHand;

    // Picks the arm preset and sizes for the weapon or fist on this object, and makes it render through portals.
    public void Start()
    {
        Component[] C = gameObject.GetComponents<Component>();
        foreach (Component _C in C)
            switch (_C)
            {
                case Revolver _:
                case FishingRodWeapon _:
                    {
                        Arm = Arm ?? Arm.FeedbackerPreset(transform);
                        ArmSize = new Vector3(1, 1, 1);
                        HandSize = new Vector3(100, 100, 100);
                        break;
                    }
                case Sandbox.Arm.SandboxArm _:
                    {
                        Arm = Arm ?? Arm.SandboxerPreset(transform);
                        ArmSize = new Vector3(1, 1, 1);
                        HandSize = new Vector3(100, 100, 100);
                        break;
                    }
                case Punch _:
                    {
                        OnArmHand = true;
                        ArmSize = new Vector3(.001f, .001f, .001f);
                        switch (GetComponent<Punch>().type)
                        {
                            case FistType.Standard:
                                {
                                    Arm = Arm ?? Arm.FeedbackerPreset(transform);
                                    HandSize = new Vector3(325, 325, 325) * 100;
                                    break;
                                }
                            case FistType.Heavy:
                                {
                                    Arm = Arm ?? Arm.KnuckleblasterPreset(transform);
                                    HandSize = new Vector3(275, 275, 275);
                                    break;
                                }
                            case FistType.Spear:
                            default: Destroy(GetComponent<VRArmTransformer>()); break;
                        }
                        break;
                    }
                case HookArm _:
                    {
                        OnArmHand = true;
                        Arm = Arm ?? Arm.WhiplashPreset(transform);
                        ArmSize = new Vector3(.01f, .01f, .01f);
                        HandSize = new Vector3(35, 35, 35);

                        // hook fix
                        Transform HookOffset = new GameObject("Hook Offset").transform;
                        HookOffset.SetParent(Arm.Hand.Root, false);
                        HookOffset.localPosition = new Vector3(.005f, -.025f, 0);
                        HookOffset.localEulerAngles = new Vector3(-5, 80, 0);
                        HookOffset.localScale = new Vector3(.95f, .95f, .95f);
                        Arm.Forearm.GetChild(1).SetParent(HookOffset, true);
                        break;
                    }
            }
        // Left handed, the arm is on the right controller, so its left arm model is mirrored into a right one. Gun hand
        // items are not: a flip of their own x axis turned the revolver around (it pointed up, away from the hand), and
        // the other guns are not mirrored either.
        if (Vars.Config.Controllers.LeftHanded && OnArmHand)
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);


        if (!GetComponent<PortalAwareRenderer>())
            gameObject.AddComponent<PortalAwareRenderer>().objectType = PortalAwareRenderer.ObjectType.Player;
    }

    // Applies the arm and hand scales each frame.
    public void LateUpdate()
    {
        if (Arm != null)
        {
            Arm.GameObject.localScale = transform.localScale;
            Arm.Root.localScale = ArmSize;
            Arm.Hand.Root.localScale = HandSize;
        }
    }
}
