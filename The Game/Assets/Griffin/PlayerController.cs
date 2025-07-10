#define PLAYERCONTROLLER_INERTIA
#define PLAYERCONTROLLER_DIRECTIONAL_SPEED

using System;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[Serializable]
public struct GroundState
{
    public float Distance;
    public bool NearGround;
    public bool Grounded;
    [CanBeNull] public SoundEmitterSettings SoundSettings;
}

public class PlayerController : MonoBehaviour, IDamagable
{
    public GameObject Camera;
    
    [Range(0.01f, 10.0f)] 
    public float MouseSensitivity;
    public Vector2 RotationClamp = new(-90.0f, 90.0f);
    public int MaximumHealth = 100;

    [Header("Stamina")]
    public float MaximumStamina = 1.0f;
    public float StaminaRecoveryTime = 0.5f;
    public float StandingRecoveryRate = 1.0f;
    
    [Header("Walking")]
    public float WalkingSpeed = 1.34f;
    public float WalkingRecoveryMultiplier = 0.5f;
    public float StandingHeight = 2.0f;
    public float FootstepOffset;
    
    [Header("Running")]
    public float RunningSpeed = 5.0f;
    public float RunningDepletionRate = 0.5f;
    
    [Header("Crouching")]
    public float CrouchingSpeed = 0.7f;
    public float CrouchingRecoveryRate = 2.0f;
    public float GroundSnapTime = 0.1f;
    public float CrouchTime = 0.2f;
    public float CrouchingHeight = 1.0f;
    
    [Header("Inertia")]
    public float Acceleration = 7.0f;
    public float Deacceleration = 15.0f;

    public float StaminaRelative => _stamina / MaximumStamina;
    public int Health => (int) _health;
    public float HealthRelative => Mathf.Floor(_health) / MaximumHealth;
    public bool IsDead => Health == 0;

    private bool _moving => _ground.NearGround && _realVelocity.sqrMagnitude > 0.01;

    private Vector3 _velocity, _previousPosition, _realVelocity;
    
    private float _rotationX, _rotationY;
    private CharacterController _controller;
    private GroundState _ground;
    private float _fallingTime;
    private float _stamina, _staminaRecoveryTimer;
    private bool _running, _crouch;
    private float _health;
    
    private float _standingTimer, _footstepOffset;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _footstepAudioSource;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
        _stamina = MaximumStamina;
        _health = MaximumHealth;

        _previousPosition = transform.position;
    }
    
    void Update()
    {
        GetRealVelocity();
        _ground = GetGround();
        _crouch = ShouldCrouch();
        _running = GetRunning();
        GetStandingTime();
        
        CalculateVelocity();
        CalculateRotation();

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
            if(_ground.SoundSettings is not null)
            {
                _footstepAudioSource.clip = _ground.SoundSettings.Footstep;
                _footstepAudioSource.Play();
            }
            _footstepOffset %= FootstepOffset;
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
        
        _footstepOffset += new Vector2(_realVelocity.x, _realVelocity.z).magnitude * Time.deltaTime;
    }

    void CalculateRotation()
    {
        float x = Input.GetAxis("Mouse X"), // Left to Right
              y = Input.GetAxis("Mouse Y"); // Down to Up

        _rotationX += x * MouseSensitivity;
        _rotationY = Mathf.Clamp(_rotationY + y * MouseSensitivity, RotationClamp.x, RotationClamp.y);
        
        transform.localRotation = Quaternion.Euler(0.0f, _rotationX, 0.0f);
        Camera.transform.localRotation = Quaternion.Euler(-_rotationY, 0.0f, 0.0f);
    }

    float GetSpeed()
    {
        if (_crouch)
            return CrouchingSpeed;
        
        return _running ? RunningSpeed : WalkingSpeed;
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
        _health -= damage;
        _health = Mathf.Clamp(_health, 0.0f, MaximumHealth);
    }

    public GameObject GameObject()
    {
        return gameObject; 
    }

    private GroundState GetGround()
    {
        GroundState result = new GroundState
        {
            Grounded = _controller.isGrounded,
            NearGround = _controller.isGrounded
        };

        Vector3 rayOrigin = transform.position + Vector3.down * (_controller.height / 2.0f);
        
        RaycastHit rayResult;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out rayResult, _controller.stepOffset))
            return result;
        
        Debug.DrawRay(rayOrigin, Vector3.down * rayResult.distance, Color.red);

        result.Distance = Mathf.Max(rayResult.distance - _controller.skinWidth, 0.0f);
        result.NearGround |= result.Distance < _controller.stepOffset;
        
        GroundSoundProfile profile = rayResult.collider.GetComponent<GroundSoundProfile>();
        if (profile is not null)
            result.SoundSettings = profile.GetSettings();
        
        return result;
    }

    private bool GetRunning()
    {
        _staminaRecoveryTimer += Time.deltaTime;
        return Input.GetKey(KeyCode.LeftShift) && _stamina > 0.0f && !_crouch;
    }

    private float GetStaminaRecoveryRate()
    {
        bool running = _running && _ground.NearGround && _realVelocity.magnitude > WalkingSpeed * 1.01; // 1.01 as epsilon.
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
        return Physics.SphereCast(transform.position, _controller.radius, Vector3.up, out RaycastHit hit, heightDifference, ~gameObject.layer);
    }

    private void GetStandingTime()
    {
        _standingTimer += Time.deltaTime;
        if (_moving)
            _standingTimer = 0.0f;
    }

    private void GetRealVelocity()
    {
        _realVelocity = (transform.position - _previousPosition) / Time.deltaTime;
        _previousPosition = transform.position;
    }
}
