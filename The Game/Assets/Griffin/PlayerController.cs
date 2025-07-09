#define PLAYERCONTROLLER_USE_INERTIA

using System;
using System.Collections;
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

    private Vector3 _velocity;
    private float _rotationX, _rotationY;
    private CharacterController _controller;
    private GroundState _ground;
    private float _fallingTime;
    private float _stamina, _staminaRecoveryTimer;
    private bool _running;
    private float _health;
    private bool _poisoned;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
        _stamina = MaximumStamina;
        _health = MaximumHealth;
    }
    
    void Update()
    {
        _ground = GetGround();
        _running = GetRunning();
        
        CalculateVelocity();
        CalculateRotation();

        _stamina += GetStaminaRecoveryRate() * Time.deltaTime;
        _stamina = Mathf.Clamp(_stamina, 0.0f, MaximumStamina);

        float originalHeight = _controller.height;
        float targetHeight = Input.GetKey(KeyCode.C) ? CrouchingHeight : StandingHeight;
        
        if(!Mathf.Approximately(_controller.height, targetHeight))
        {
            _controller.height = Mathf.MoveTowards(_controller.height, targetHeight, 1.0f / CrouchTime * Time.deltaTime);
            _controller.Move((_controller.height - originalHeight) * 0.5f * Vector3.up);
        }

    #if PLAYERCONTROLLER_USE_INERTIA
        _controller.Move(_velocity * Time.deltaTime);
    #else
        _controller.Move(transform.rotation * _velocity * Time.deltaTime);
    #endif
    }

    void CalculateVelocity()
    {
        // _ground.Grounded has an extended hit range to help with walking down slopes.
        float targetSpeed = GetSpeedDirectional();
        float speedDifference = (_velocity.magnitude - targetSpeed) / targetSpeed;
        
        if (!_ground.Grounded)
        {
            if (_ground.NearGround && _fallingTime <= GroundSnapTime) 
                _controller.Move(Vector3.down * _ground.Distance);
     
            _velocity += Physics.gravity * Time.deltaTime;
            _fallingTime += Time.deltaTime;
        }
        else
            _velocity.y = 0.0f;

        if (!_ground.NearGround)
            return;
        
        if(_ground.Grounded || _fallingTime < GroundSnapTime)
            _fallingTime = 0.0f;
        
    #if PLAYERCONTROLLER_USE_INERTIA
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
            _velocity *= Mathf.Clamp01(1.0f - (Deacceleration * Time.deltaTime * speedDifference));
        
        direction.Normalize();
        _velocity += direction * (Acceleration * Time.deltaTime);
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
        if (Input.GetKey(KeyCode.C))
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
        
        SoundEmitter emitter = rayResult.collider.GetComponent<SoundEmitter>();
        if (emitter is not null)
            result.SoundSettings = emitter.GetSettings();
        
        return result;
    }

    private bool GetRunning()
    {
        _staminaRecoveryTimer += Time.deltaTime;
        return Input.GetKey(KeyCode.LeftShift) && _stamina > 0.0f;
    }

    private float GetStaminaRecoveryRate()
    {
        bool running = _running && _ground.NearGround && _velocity.magnitude > WalkingSpeed * 1.01; // 1.01 as epsilon.
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

        bool moving = _ground.NearGround && _velocity.sqrMagnitude > 0.01;
        if (moving)
            result *= WalkingRecoveryMultiplier;
        
        return result;
    }

    public void ApplyPoison(DamageType.PoisonData data)
    {
        if (!_poisoned)
            StartCoroutine(PoisonOverTime(data));
    }

    IEnumerator PoisonOverTime(DamageType.PoisonData data)
    {
        _poisoned = true;
        float elapsed = 0f;

        while (elapsed < data.duration)
        {
            OnTakeDamage(new DamageSource { Name = "Poison", Object = null }, data.damagePerTick);
            yield return new WaitForSeconds(data.tickRate);
            elapsed += data.tickRate;
        }

        _poisoned = false;
    }
}
