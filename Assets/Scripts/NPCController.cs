using UnityEngine;

// a little dumb npc brain that follows a target
public class NPCController : ThirdPersonCharacterController
{
    private ThirdPersonCharacter character;
    
    public Transform target; // where to go; what to follow
    
    public float stopDistance = 1, runAfterDistance = 5;
    private bool targetInProximity;
    
    private bool sprinting;
    private Vector2 movement;

    public override event Jump onJump;
    
    private void Start()
    {
        character = GetComponent<ThirdPersonCharacter>();
        character.CameraRelativeMovement = false;
    }

    private void Update()
    {
        var distanceToTarget = Vector3.Distance(transform.position, target.position);
        var directionToTarget = target.position - transform.position;
        directionToTarget = new Vector3(directionToTarget.x,  directionToTarget.z, 0);
        directionToTarget.Normalize();

        if(distanceToTarget > stopDistance)
        {
            movement = directionToTarget;
            sprinting = (distanceToTarget > runAfterDistance);
            targetInProximity = false;
        }
        else
        {
            movement = Vector2.zero;
            targetInProximity = true;
        }

        if(character.WallCollision) onJump();

        if(targetInProximity)
        {
            // in proximity to the target; attack or ... give a hug or something!
        }

    }

    public override bool sprint() => sprinting;

    public override Vector2 move() => movement;
}
