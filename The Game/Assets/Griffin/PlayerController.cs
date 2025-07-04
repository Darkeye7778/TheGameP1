#define PLAYERCONTROLLER_USE_INERTIA

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
    public Vector2 RotationClamp = new Vector2(-90.0f, 90.0f);
    
    public float WalkingSpeed = 1.34f;
    public float RunningSpeed = 5.0f;
    public float CrouchingSpeed = 0.7f;
    public float Acceleration = 7.0f;
    public float Deacceleration = 15.0f;
    
    public float StandingHeight = 2.0f;
    public float CrouchingHeight = 1.0f;
    
    public float GroundSnapTime = 0.1f;
    public float CrouchTime = 0.2f;

    private Vector3 _velocity;
    private float _rotationX, _rotationY;
    private CharacterController _controller;
    private GroundState _ground;
    private float _fallingTime;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
    }
    
    void Update()
    {
        _ground = GetGround();
        
        CalculateVelocity();
        CalculateRotation();

        float originalHeight = _controller.height;
        float targetHeight = Input.GetKey(KeyCode.C) ? CrouchingHeight : StandingHeight;
        
        if(!Mathf.Approximately(_controller.height, targetHeight))
        {
            _controller.height =
                Mathf.MoveTowards(_controller.height, targetHeight, 1.0f / CrouchTime * Time.deltaTime);

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
        
        float speedDifference = (_velocity.magnitude - GetSpeed()) / GetSpeed();
        
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
        else if(speedDifference > 0.0f)
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
        
        return Input.GetKey(KeyCode.LeftShift) ? RunningSpeed : WalkingSpeed;
    }

    public void OnTakeDamage(float damage)
    {
        throw new System.NotImplementedException();
    }

    public void OnDeath()
    {
        throw new System.NotImplementedException();
    }

    public GroundState GetGround()
    {
        GroundState result = new GroundState();

        result.Grounded = _controller.isGrounded;
        result.NearGround = result.Grounded;

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
}
