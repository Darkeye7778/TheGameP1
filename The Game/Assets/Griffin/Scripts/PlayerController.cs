#define PLAYERCONTROLLER_INERTIA
#define PLAYERCONTROLLER_DIRECTIONAL_SPEED
#define PLAYERCONTROLLER_AUTOCROUCH

using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct GroundState
{
    public float Distance;
    public bool NearGround;
    public bool Grounded;
    public MaterialSettings SoundSettings;
    
    public static GroundState GetGround(Vector3 origin, float maxDistance, LayerMask groundMask)
    {
        GroundState result = new GroundState
        {
            Grounded = false,
            NearGround = false,
            SoundSettings = SoundManager.Instance.DefaultSoundProfile
        };
        
        RaycastHit rayResult;
        if (!Physics.Raycast(origin, Vector3.down, out rayResult, maxDistance, groundMask))
            return result;
        
        Debug.DrawRay(origin, Vector3.down * rayResult.distance, Color.red);

        result.Distance = Mathf.Max(rayResult.distance, 0.0f);
        result.NearGround |= result.Distance < maxDistance;
        
        MaterialProfile profile = rayResult.collider.GetComponent<MaterialProfile>();
        if (profile is not null)
            result.SoundSettings = profile.GetSettings();
    
        return result;
    }
}

public class PlayerController : MonoBehaviour, IDamagable
{
    public GameObject Camera;
    public WeaponRotationPivot Pivot;
    public IKSolver IK;
    
    [Range(0.01f, 10.0f)] 
    public float MouseSensitivity;
    public Vector2 RotationClamp = new(-90.0f, 90.0f);
    public int MaximumHealth = 100;
    public LayerMask GroundMask;
    public LayerMask InteractSkip;
    public int Health => (int) _health;
    public bool TookDamage => _health < _previousHealth;
    public bool GainedHealth => _health > _previousHealth;
    public bool IsDead => Health <= 0;
    public float HealthRelative => Mathf.Floor(_health) / MaximumHealth;
    public float Height => _controller.height;

    [Header("Hitbox")]
    public Animator Animator;
    public Transform[] HitPoints;
    private Vector3[] _hitPoints;

    [Header("Leaning")]
    public bool ToggleLeaning = true;
    [FormerlySerializedAs("Angle")] public float LeanAngle = 30f;
    [FormerlySerializedAs("TransitionSpeed")] public float LeanTransitionSpeed = 1.2f;
    public bool Leaning => _leaningTarget != 0.0f;
    public bool LeaningLeft => _leaningTarget > 0.0f;
    public bool LeaningRight => _leaningTarget < 0.0f;

    [Header("Stamina")]
    public float MaximumStamina = 1.0f;
    public float StaminaRecoveryTime = 0.5f;
    public float StandingRecoveryRate = 1.0f;
    public float StaminaRelative => _stamina / MaximumStamina;
    
    [Header("Walking")]
    public float WalkingSpeed = 1.34f;
    public float WalkingRecoveryMultiplier = 0.5f;
    public float StandingHeight = 2.0f;
    public float FootstepOffset;
    public float SoundRadius = 1f;
    
    [Header("Running")]
    public float RunningSpeed = 5.0f;
    public float RunningDepletionRate = 0.5f;
    public float RunningSoundRadius = 2f;
    
    [Header("Crouching")]
    public float CrouchingSpeed = 0.7f;
    public float CrouchingRecoveryRate = 2.0f;
    public float GroundSnapTime = 0.1f;
    public float CrouchTime = 0.2f;
    public float CrouchingHeight = 1.0f;
    public float CrouchSoundRadius = 0.5f;
    
    [Header("Inertia")]
    public float Acceleration = 7.0f;
    public float Deacceleration = 15.0f;
    public Vector3 RealVelocity { get; private set; }
    public Vector3 LocalRealVelocity { get; private set; }
    public bool HasTraction => _fallingTime <= GroundSnapTime;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _footstepAudioSource;
    
    public GameObject CurrentInteractable { get; private set; }
    
    private bool _moving => _ground.NearGround && RealVelocity.sqrMagnitude > 0.01;
    Vector3 _velocity, _previousPosition;

    private float _rotationX, _rotationY;
    private CharacterController _controller;
    private GroundState _ground;
    private float _fallingTime;
    private float _stamina, _staminaRecoveryTimer;
    private bool _running, _crouch;
    private float _health = 1;
    private float _previousHealth;
    
    private float _standingTimer, _footstepOffset;
    private float _leaningAngle, _leaningTarget;
    
    private float _recoilOffsetX, _recoilOffsetY;
    private float _invulnUntilUnscaled = 0f;

    private Vector3 _cameraOrigin, _cameraTarget;
    private Vector3 _eyeWeaponOffset;
 
    private PlayerInventory _inventory;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
        _stamina = MaximumStamina;
        _health = _previousHealth = MaximumHealth;
        _controller.height = StandingHeight;
        _inventory = GetComponent<PlayerInventory>();
        //_cameraOrigin = Camera.transform.localPosition;
        _previousPosition = transform.position;
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            gameManager.instance.ShowLoadouts();
        }
        if (Time.timeScale == 0.0f)
            return;

        _previousHealth = _health;

        _cameraOrigin = _controller.height / 2 * Vector3.up;
        
        GetRealVelocity();
        _ground = GetGround();
        _crouch = ShouldCrouch();
        _running = GetRunning();
        GetStandingTime();
        CheckInteract();
        CalculateVelocity();
        CalculateRotation();
        CalculateLeaning();

        if (Input.GetKeyDown(KeyCode.Alpha0))
            _health = MaximumHealth = 1000000;
        
        Animator.SetFloat("Speed",  Mathf.Max(LocalRealVelocity.magnitude, 1) * 0.5f, 0.1f, Time.deltaTime);
        Animator.SetFloat("Velocity X", LocalRealVelocity.x, 0.1f, Time.deltaTime);
        Animator.SetFloat("Velocity Y", LocalRealVelocity.z, 0.1f, Time.deltaTime); 
        Animator.SetBool("Crouching", _crouch);

        if (_hitPoints == null || _hitPoints.Length != HitPoints.Length)
            _hitPoints = new Vector3[HitPoints.Length];
        for (int i = 0; i < HitPoints.Length; i++)
            _hitPoints[i] = HitPoints[i].position;
        
        _stamina += GetStaminaRecoveryRate() * Time.deltaTime;
        _stamina = Mathf.Clamp(_stamina, 0.0f, MaximumStamina);
        
        float originalHeight = _controller.height;
        float targetHeight = _crouch ? CrouchingHeight : StandingHeight;
        
        if(!Mathf.Approximately(_controller.height, targetHeight))
        {
            _controller.height = Mathf.MoveTowards(_controller.height, targetHeight, 1.0f / CrouchTime * Time.deltaTime);
            _controller.Move((_controller.height - originalHeight) * 0.5f * Vector3.up);
        }

        if (_standingTimer > 0.1)
            _footstepOffset = 0.0f;
        
        if (_moving && _footstepOffset >= FootstepOffset)
        {
            _footstepAudioSource.clip = _ground.SoundSettings.Footstep.PickSound();
            _footstepAudioSource.volume = _ground.SoundSettings.Footstep.Volume;
            _footstepAudioSource.Play();
            _footstepOffset %= FootstepOffset;

            float multiplier = SoundRadius;
            if (_crouch) multiplier *= CrouchSoundRadius;
            if (_running) multiplier *= RunningSoundRadius;
            
            SoundManager.Instance.EmitSound(new SoundInstance(_ground.SoundSettings.Footstep,  _footstepAudioSource.clip, gameObject, multiplier));
        }

    #if PLAYERCONTROLLER_INERTIA
        _controller.Move(_velocity * Time.deltaTime);
    #else
        _controller.Move(transform.rotation * _velocity * Time.deltaTime);
    #endif
    }

    void CalculateVelocity()
    {
        // _ground.Grounded has an extended hit range to help with walking down slopes.
    #if PLAYERCONTROLLER_DIRECTIONAL_SPEED
        float targetSpeed = GetSpeedDirectional();
    #else
        float targetSpeed = GetSpeed();
    #endif

        float speedDifference = Mathf.Clamp01((_velocity.magnitude - targetSpeed) / targetSpeed);
        
        if (!_ground.Grounded)
        {
            if (_ground.NearGround && _fallingTime <= GroundSnapTime)
                _controller.Move(Vector3.down * _ground.Distance);
     
            _velocity += Physics.gravity * Time.deltaTime;
        }
        else
            _velocity.y = 0.0f;

        if (!_ground.NearGround)
        {
            _fallingTime += Time.deltaTime;
            return;
        }
        
        if(_ground.Grounded || _fallingTime < GroundSnapTime)
            _fallingTime = 0.0f;
        
    #if PLAYERCONTROLLER_INERTIA
        Vector3 direction = Input.GetAxisRaw("Vertical") * transform.forward +
                            Input.GetAxisRaw("Horizontal") * transform.right;
    #else
        Vector3 direction = Input.GetAxisRaw("Vertical") * Vector3.forward +
                            Input.GetAxisRaw("Horizontal") * Vector3.right;
    #endif

        // Slows down the character when not actively moving.
        if (direction.sqrMagnitude == 0.0f)
            _velocity *= 1.0f - (Deacceleration * Time.deltaTime);
        // Slows down the character when over target speed.
        else if (speedDifference > 0.0f)
            _velocity *= 1.0f - (Deacceleration * Time.deltaTime * speedDifference);
        
        direction.Normalize();
        
        if(_ground.NearGround)
            _velocity += direction * (Acceleration * Time.deltaTime);
        
        _footstepOffset += RealVelocity.magnitude * Time.deltaTime;
    }

    void CalculateRotation()
    {
        float x = Input.GetAxis("Mouse X"), // Left to Right
              y = Input.GetAxis("Mouse Y"); // Down to Up

        _rotationX += x * MouseSensitivity;
        _rotationY = Mathf.Clamp(_rotationY + y * MouseSensitivity, RotationClamp.x, RotationClamp.y);

        if (Pivot)
            Pivot.Rotation = _rotationY;

        if (IK)
        {
            Vector3 lookTarget = Pivot.FinalRotation * Vector3.forward + _inventory.Eye.position;
            
            IK.LookAtWeight = 1;
            IK.LookAt = lookTarget;
        }

        transform.localRotation = Quaternion.Euler(0.0f, _rotationX, 0.0f);
        Camera.transform.localRotation = Quaternion.Euler(-_rotationY, 0, _leaningAngle);
    }

    void CalculateLeaning()
    {
        //Leaning has the option to be toggled or held.
        if (ToggleLeaning)
        {
            //leaning will reset when the same key is pressed again.
            if (Input.GetKeyDown(KeyCode.Q)) // Left
            {
                if (!LeaningLeft)
                    _leaningTarget = LeanAngle;
                else
                    _leaningTarget = 0.0f;
            }
            else if (Input.GetKeyDown(KeyCode.E)) // Right
            {
                if (!LeaningRight)
                    _leaningTarget = -LeanAngle;
                else
                    _leaningTarget = 0.0f;
            }
        }
        else
        {
            if (Input.GetKey(KeyCode.Q))
                _leaningTarget = LeanAngle;
            else if (Input.GetKey(KeyCode.E))
                _leaningTarget = -LeanAngle;
            else
                _leaningTarget = 0.0f;
        }
        // Smoothly transition the leaning angle towards the target.
        _leaningAngle = Mathf.MoveTowards(_leaningAngle, _leaningTarget, LeanTransitionSpeed * 100f * Time.deltaTime);


        // Calculate the camera target position based on leaning.
        float cameraOffset;
        if (Leaning)
            cameraOffset = -_leaningTarget * 0.03f;
        else
            cameraOffset = 0;
        
        _cameraTarget = new Vector3(_cameraOrigin.x + cameraOffset, _cameraOrigin.y, _cameraOrigin.z);

        Camera.transform.localPosition = Vector3.Lerp(Camera.transform.localPosition, _cameraTarget, LeanTransitionSpeed * 10.0f * Time.deltaTime);
    }
    
    float GetSpeed()
    {
        if (_crouch)
            return CrouchingSpeed;
        
        return _running ? RunningSpeed : WalkingSpeed;
    }
    
    void CheckInteract()
    {
        if (Physics.Raycast(Camera.transform.position, Camera.transform.forward, out RaycastHit hit, 3.0f, ~InteractSkip))
        {
            if (hit.collider.TryGetComponent(out Interactable interactable))
            {
                CurrentInteractable = hit.collider.gameObject;
                if (Input.GetKeyDown(KeyCode.F))
                {
                    interactable.OnInteract(gameObject);
                }
            }
            else
            {
                CurrentInteractable = null;
            }
        }
        else
        {
            CurrentInteractable = null;
        }
    }

    float GetSpeedDirectional()
    {
        float targetSpeed = GetSpeed();
        if (!_running)
            return targetSpeed;
        
        float dotProduct = Mathf.Clamp01(Vector3.Dot(_velocity, transform.forward));
        return Mathf.Lerp(WalkingSpeed, targetSpeed, dotProduct);
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        if (Time.unscaledTime < _invulnUntilUnscaled) return; 
        _health = Mathf.Clamp(_health - damage, 0f, MaximumHealth);
    }
    
    public void ResetState()
    {
        _health = MaximumHealth;
        _stamina = MaximumStamina;

        _previousPosition = transform.position;
        
        _previousHealth = _health;
        _fallingTime = 0;
        _standingTimer = 0;
        _footstepOffset = 0;
        _leaningTarget = 0;
        _leaningAngle = 0;
        _running = false;
        _crouch = false;
        
        Animator.SetTrigger("Reset");

        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        _controller.height = StandingHeight;
        _controller.Move(Vector3.zero); // clear movement force
    }

    public GameObject GameObject()
    {
        return gameObject; 
    }

    public Vector3[] AimTargets()
    {
        return _hitPoints;
    }

    private GroundState GetGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.down * (_controller.height / 2.0f);
        
        GroundState result = GroundState.GetGround(rayOrigin, _controller.stepOffset, GroundMask);
        result.Grounded |= _controller.isGrounded;
        result.NearGround |= _controller.isGrounded;
        
        return result;
    }

    private bool GetRunning()
    {
        _staminaRecoveryTimer += Time.deltaTime;
        return Input.GetKey(KeyCode.LeftShift) && _stamina > 0.0f && !_crouch;
    }

    private float GetStaminaRecoveryRate()
    {
        bool running = _running && _ground.NearGround && RealVelocity.magnitude > WalkingSpeed * 1.01; // 1.01 as epsilon.
        if (running)
        {
            _staminaRecoveryTimer = 0.0f;
            return -RunningDepletionRate;
        }
        
        if (_staminaRecoveryTimer < StaminaRecoveryTime) 
            return 0.0f;
        
        float result = StandingRecoveryRate;

        if (Input.GetKey(KeyCode.C))
            result *= CrouchingRecoveryRate;

        if (_moving)
            result *= WalkingRecoveryMultiplier;
        
        return result;
    }
    
    private bool ShouldCrouch()
    {
        if (Input.GetKey(KeyCode.C))
            return true;

        float heightDifference = StandingHeight - _controller.height / 2.0f - _controller.radius;
        // I cannot get Physics.CheckCapsule to work.
    #if PLAYERCONTROLLER_AUTOCROUCH
        return Physics.SphereCast(transform.position, _controller.radius, Vector3.up, out RaycastHit hit, heightDifference, GroundMask);
    #else
        return false;
    #endif
    }

    private void GetStandingTime()
    {
        _standingTimer += Time.deltaTime;
        if (_moving)
            _standingTimer = 0.0f;
    }

    private void GetRealVelocity()
    {
        RealVelocity = (transform.position - _previousPosition) / Time.deltaTime;
        LocalRealVelocity = transform.InverseTransformDirection(RealVelocity);
        _previousPosition = transform.position;

        RealVelocity = FixNan(RealVelocity);
        LocalRealVelocity = FixNan(LocalRealVelocity);
    }

    private static Vector3 FixNan(Vector3 vec)
    {
        if (float.IsNaN(vec.x)) vec.x = 0;
        if (float.IsNaN(vec.y)) vec.y = 0;
        if (float.IsNaN(vec.z)) vec.z = 0;
        return vec;
    }

    public void GrantTemporaryInvulnerability(float seconds)
    {
        _invulnUntilUnscaled = Mathf.Max(_invulnUntilUnscaled, Time.unscaledTime + Mathf.Max(0f, seconds));
    }
    public Vector3 LookTarget()
    {
        return _inventory.Eye.position;
    }

    bool IDamagable.IsDead()
    {
        return IsDead;
    }
}
