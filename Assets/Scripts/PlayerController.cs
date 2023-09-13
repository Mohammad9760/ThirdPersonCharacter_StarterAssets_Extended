using System.Collections;
using UnityEngine;
using Cinemachine;
// de-coupling the third person character from the input handeling and camera work
public class PlayerController : ThirdPersonCharacterController
{
    private ThirdPersonCharacter PlayerCharacter;

    #region exposed properties
    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;

    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;

    [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    #endregion
    
    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private float crouchingCameraZoom = 0.5f;

    [SerializeField] private CinemachineVirtualCamera FollowCamera, AimCamera;
    private CinemachineVirtualCamera activeCamera => ADS? AimCamera: FollowCamera;
    private CinemachineBasicMultiChannelPerlin followCameraNoise;
    public NoiseSettings sprintingNoiseProfile, walkNoiseProfile, idleNoiseProfile;

    private bool _ads;
    public bool ADS
    {
        get => _ads;

        private set
        {
            _ads = value;
            strafeToggle();
            AimCamera.gameObject.SetActive(value);
        }
    }

    private Controls _input;

    public override event Jump onJump;
    public override event Strafe strafeToggle;
    public override event Crouch crouchToggle;

    public override bool sprint() => 0 != _input.Player.Sprint.ReadValue<float>();

    public override Vector2 move() => _input.Player.Move.ReadValue<Vector2>();

    public Vector2 look() => _input.Player.Look.ReadValue<Vector2>();

    private void Awake()
    {
        _input = new Controls();
        _input.Enable();
        _input.Player.Jump.started += (ctx) => onJump();
        _input.Player.Strafe.performed += (ctx) => ADS = !ADS;
        _input.Player.Crouch.performed += (ctx) => crouchToggle();
        // we don't want a mouse cursor like...ever
        Cursor.lockState = CursorLockMode.Locked;

        PlayerCharacter = GetComponent<ThirdPersonCharacter>();
        PlayerCharacter.CameraRelativeMovement = true;
        followCameraNoise = FollowCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        crouchToggle += () => StartCoroutine(crouchCameraDistanceAnimation());
         
    }

    private IEnumerator crouchCameraDistanceAnimation()
    {
        var comp = FollowCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        var cur = comp.CameraDistance;
        var target = comp.CameraDistance + (PlayerCharacter.Crouching? -crouchingCameraZoom: crouchingCameraZoom);

        for(float t = 0; t < 1; t += Time.deltaTime * 3)
        {
            comp.CameraDistance = Mathf.Lerp(cur, target, t * t);
            yield return null;
        }
    }

    private const float _threshold = 0.01f;

    private bool IsCurrentDeviceMouse
    {
        get
        {
            return true;
        }
    }

    private void Start()
    {
        _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        // activeCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>().CameraSide = 1;
    }

    private void LateUpdate()
    {
        CameraRotation();
        // update camera noise based on character movement
        followCameraNoise.m_NoiseProfile = PlayerCharacter.Idle | PlayerCharacter.WallCollision? idleNoiseProfile: PlayerCharacter.Sprint? sprintingNoiseProfile: walkNoiseProfile;

    }

    private void CameraRotation()
    {
        // if there is an input and camera position is not fixed
        if (look().sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += look().x * deltaTimeMultiplier;
            _cinemachineTargetPitch += look().y * deltaTimeMultiplier;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }


}
