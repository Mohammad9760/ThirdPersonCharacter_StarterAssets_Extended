using UnityEngine;

#pragma warning disable // I get warned that the virtual events in this class in never used... yeah and!?

[RequireComponent(typeof(ThirdPersonCharacter))]
public class ThirdPersonCharacterController : MonoBehaviour
{
    public delegate void Jump();
    public virtual event Jump onJump;
    public delegate void Strafe();
    public virtual event Strafe strafeToggle;
    public delegate void Crouch();
    public virtual event Crouch crouchToggle;

    public virtual bool sprint() => false;
    public virtual Vector2 move() => Vector2.zero;
}
