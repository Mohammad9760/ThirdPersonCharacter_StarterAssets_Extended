 using UnityEngine;

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonCharacter : MonoBehaviour
{
    #region exposed properties
    [Header("Character")]
    [Tooltip("Move speed of the character in m/s")]
    public float MoveSpeed = 2.0f;

    [Tooltip("Sprint speed of the character in m/s")]
    public float SprintSpeed = 5.335f;

    [Tooltip("Move speed in m/s when strafing")]
    public float StrafeSpeed = 1.8f;

    [Tooltip("Sprint speed in m/s when strafing")]
    public float StrafeRunSpeed = 4f;

    [Tooltip("Crouching speed in m/s")]
    public float CrouchSpeed = 1.5f;

    [Tooltip("How fast the character turns to face movement direction")]
    [Range(0.0f, 0.3f)]
    public float RotationSmoothTime = 0.12f;

    [Tooltip("Acceleration and deceleration")]
    public float SpeedChangeRate = 10.0f;

    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Space(10)]
    [Tooltip("The height the player can jump")]
    public float JumpHeight = 1.2f;

    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
    public float JumpTimeout = 0.50f;

    [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
    public float FallTimeout = 0.15f;

    [Tooltip("How many times/second to turn the character to face forward")]
    public float TurnInPlaceTimeout = 2;

    [Tooltip("Useful for rough ground")]
    public float GroundedOffset = -0.14f;

    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float GroundedRadius = 0.28f;

    [Tooltip("What layers the character uses as ground")]
    public LayerMask GroundLayers;

    #endregion

    // character
    private float _speed;
    private float _animationBlend;
    private Vector3 _animationBlendStrafe;
    private float _animationTurnInPlace;
    private float _targetRotation = 0.0f;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _terminalVelocity = 53.0f;

    // timeout deltatime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;
    private float _nextTurnTime = 0;

    // animation IDs
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;
    private int _animIDStrafeToggle, _animIDStrafeSpeedX, _animIDStrafeSpeedY, _animIDTurnInPlace;
    private int _animIDCrouch;

    private Animator _animator;
    private CharacterController _controller;
    private bool _hasAnimator => TryGetComponent(out _animator);

    // how this third person character is controlled
    public ThirdPersonCharacterController brain;
    [HideInInspector] public bool CameraRelativeMovement;
    private GameObject _mainCamera;

    // detecting if the character is walking into the environment walls/ obstacles
    public float wallCollisionAngleThreshold = 0.9f, wallCollisionDistanceThreshold = 0.16f;
    private Vector3? collisionPoint = null;
    public bool WallCollision
    {
        // returns true if we are trying to go (targetDirection) towards a collision
        get
        {
            if(collisionPoint == null) return false;

            if(Vector3.Dot(((Vector3)collisionPoint - transform.position).normalized, targetDirection) >= wallCollisionAngleThreshold)
            {
                if(Vector3.Distance(transform.position, (Vector3)collisionPoint) <= wallCollisionDistanceThreshold)
                    return true;
                else
                {
                    collisionPoint = null;
                    return false;
                }
            }
            else return false;
        }
    }

    // character speed and direction
    private Vector3 inputDirection => new Vector3(brain.move().x, 0.0f, brain.move().y).normalized;
    private float inputMagnitude => 1; //_input.analogMovement ? _input.move.magnitude : 1f;
    private float baseSpeed, sprintMultiplier, strafeMultiplier, crouchMultiplier;
    // private float targetSpeed => Idle || WallCollision? 0: Strafing? Sprint? StrafeRunSpeed: StrafeSpeed : Sprint ? SprintSpeed : MoveSpeed; 
    private float targetSpeed => baseSpeed * (Strafing? strafeMultiplier: 1) * (Sprint? sprintMultiplier: 1) * (Crouching? crouchMultiplier: 1) * (Idle || WallCollision? 0: 1);
    private Vector3 targetDirection => Quaternion.Euler(0.0f, _targetRotation, 0.0f) * (Strafing? inputDirection.normalized: Vector3.forward);

    // different states of the character
    public bool Crouching;
    public bool Strafing;
    public bool Sprint => brain.sprint() & !Crouching;
    public bool Idle => brain.move() == Vector2.zero;
    public bool Grounded => Physics.CheckSphere(transform.position - new Vector3(0, GroundedOffset, 0), GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

    private void Awake() 
    {
        brain = GetComponent<ThirdPersonCharacterController>();
        brain.onJump += Jump;
        brain.strafeToggle += ToggleStrafing;
        // brain.strafeToggle += TurnInPlace;
        brain.crouchToggle += CrouchToggle;
        
        if(CameraRelativeMovement) // we need camera reference for camera relative movement
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
    }

    private void ToggleStrafing() => Strafing = !Strafing;

    private void AssignAnimationIDs()
    {
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDFreeFall = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        _animIDStrafeToggle = Animator.StringToHash("Strafe");
        _animIDStrafeSpeedX = Animator.StringToHash("StrafeX");
        _animIDStrafeSpeedY = Animator.StringToHash("StrafeY");
        _animIDTurnInPlace = Animator.StringToHash("TurnInPlace");
        _animIDCrouch = Animator.StringToHash("Crouch");
    }

    private void Start()
    {
        
        _controller = GetComponent<CharacterController>();

        AssignAnimationIDs();

        // reset our timeouts on start
        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;

        // speed multipliers
        baseSpeed = MoveSpeed;
        sprintMultiplier = SprintSpeed / MoveSpeed;
        strafeMultiplier = StrafeSpeed / MoveSpeed;
        crouchMultiplier = CrouchSpeed / MoveSpeed;
    }

    private void Update()
    {
        ApplyGravity();
        UpdateAnimations();
        UpdateTimeOuts();
        Move();
    }

    private void UpdateAnimations()
    {
        // update animator if using character
        if(_hasAnimator)
        {
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;
            _animator.SetFloat(_animIDSpeed, _animationBlend);

            _animator.SetBool(_animIDGrounded, Grounded);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            _animator.SetBool(_animIDFreeFall, !Grounded);
            _animator.SetBool(_animIDFreeFall, !(_fallTimeoutDelta >= 0.0f));

            _animator.SetBool(_animIDCrouch, Crouching);

            // strafing
            _animationBlendStrafe = Vector3.Lerp(_animationBlendStrafe, 
                inputDirection * targetSpeed, Time.deltaTime * SpeedChangeRate);
            // if(_animationBlendStrafe.magnitude < 0.01f) _animationBlendStrafe = Vector3.zero;
            _animator.SetBool(_animIDStrafeToggle, Strafing);
            _animator.SetFloat(_animIDStrafeSpeedX, _animationBlendStrafe.x);
            _animator.SetFloat(_animIDStrafeSpeedY, _animationBlendStrafe.z);
            _animator.SetFloat(_animIDTurnInPlace, _animationTurnInPlace);
        }
    }

    private void Move()
    {
        #region speed interpolation
        // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon
        // a reference to the players current horizontal velocity
        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

        float speedOffset = 0.1f;

        // accelerate or decelerate to target speed
        if (currentHorizontalSpeed < targetSpeed - speedOffset ||
            currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            // creates curved result rather than a linear one giving a more organic speed change
            // note T in Lerp is clamped, so we don't need to clamp our speed
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                Time.deltaTime * SpeedChangeRate);

            // round speed to 3 decimal places
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }
        #endregion

        // rotate character when moving
        if (!Idle)
        {
            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + (CameraRelativeMovement? _mainCamera.transform.eulerAngles.y: 0);
            
            if(Strafing)
                _targetRotation = Mathf.Atan2(0, 1) * Mathf.Rad2Deg + (_mainCamera.transform.eulerAngles.y);

            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

            // rotate to face input direction relative to camera position
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }

        // rotate character when strafing aka trun-in-place
        if(Idle & Strafing)
        {
            // fix this shit
            var projectedCameraForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up);
            projectedCameraForward = (projectedCameraForward * (1 / projectedCameraForward.magnitude)).normalized;

            var angleDifference = Vector3.Cross(projectedCameraForward, transform.forward).y;
            _animationTurnInPlace = angleDifference;

            if(Mathf.Abs(angleDifference) > 0.0f)
            {
                _targetRotation = Mathf.Atan2(0, 1) * Mathf.Rad2Deg + (_mainCamera.transform.eulerAngles.y);

                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
        }

        // move the player
        _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    { 
        // stop the character if walking into a collider (walls)
        
        // don't stop if the collider is beneath or if it has a rigidbody
        if (hit.moveDirection.y < -0.3f | hit.collider.attachedRigidbody != null)
            return;

        collisionPoint = new Vector3(hit.point.x, transform.position.y, hit.point.z);
    }

    private void UpdateTimeOuts()
    {
        if(Grounded)
        {
            // reset the fall timeout timer
            _fallTimeoutDelta = FallTimeout;

            // jump timeout
            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            // reset the jump timeout timer
            _jumpTimeoutDelta = JumpTimeout;

            // fall timeout
            if (_fallTimeoutDelta >= 0.0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }

        }
        // update rate for turning in place
        // if(Strafing & Idle)
        // {
        //     if(Time.time >= _nextTurnTime)
        //     {
        //         _nextTurnTime = Time.time + 1f / TurnInPlaceTimeout;
        //         TurnInPlace();
        //     }
        // }
    }

    // private void TurnInPlace()
    // {
    // }

    private void CrouchToggle()
    {
        Crouching = !Crouching;
        
        if(Crouching)
        {
            _controller.height /= 2;
            _controller.center /= 2;
        }
        else
        {
            _controller.height *= 2;
            _controller.center *= 2;
        }
    }

    private void Jump()
    {
        if(Strafing | Crouching) return; // don't have Strafe jump animations and crouching jump don't make sense

        if(WallCollision)
        {
            Climb();
            // return;
        }
        if (_jumpTimeoutDelta <= 0.0f && Grounded)
        {
            // the square root of H * -2 * G = how much velocity needed to reach desired height
            _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetTrigger(_animIDJump);
            }
        }
    }

    private void Climb()
    {
        // TO-DO: determine whether to climb or vault over and do it
        Ray ray = new Ray((Vector3)collisionPoint + new Vector3(0, 5, 0), Vector3.down);
        RaycastHit hit;
        if(Physics.Raycast(ray, out hit))
        {
            print(transform.position.y - hit.point.y);
        }
    }

    private void ApplyGravity()
    {
        if (Grounded)
        {
            // stop our velocity dropping infinitely when grounded
            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }

        }

        // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
        if (_verticalVelocity < _terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

        if (Grounded) Gizmos.color = transparentGreen;
        else Gizmos.color = transparentRed;

        // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
        Gizmos.DrawSphere(
            new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
            GroundedRadius);
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            if (FootstepAudioClips.Length > 0)
            {
                var index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], 
                    transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(LandingAudioClip, 
                transform.TransformPoint(_controller.center), FootstepAudioVolume);
        }
    }
}
