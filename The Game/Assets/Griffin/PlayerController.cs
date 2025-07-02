#define PLAYERCONTROLLER_USE_INERTIA

using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public struct GroundState
{
    public float Distance;
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
        
        _controller.height = Input.GetKey(KeyCode.C) ? CrouchingHeight : StandingHeight;

        if (Input.GetKeyDown(KeyCode.C))
            _controller.Move(Vector3.down * ((StandingHeight - CrouchingHeight) * 0.5f));
        else if(Input.GetKeyUp(KeyCode.C))
            _controller.Move(Vector3.up * ((StandingHeight - CrouchingHeight) * 0.5f));

    #if PLAYERCONTROLLER_USE_INERTIA
        _controller.Move(_velocity * Time.deltaTime);
    #else
        _controller.Move(transform.rotation * _velocity * Time.deltaTime);
    #endif
    }

    void CalculateVelocity()
    {
        float speedDifference = _velocity.sqrMagnitude - GetSpeed();
        
        if (!_controller.isGrounded)
        {
            if(_ground.Distance < _controller.stepOffset && _fallingTime < GroundSnapTime)
                _controller.Move(new Vector3(0, -_ground.Distance, 0));
            
            _velocity += Physics.gravity * Time.deltaTime;
            _fallingTime += Time.deltaTime;
        }
        else
        {
            _fallingTime = 0.0f;
            _velocity.y = 0.0f;
        }
        
        if (!_ground.Grounded)
            return;
        
    #if PLAYERCONTROLLER_USE_INERTIA
        Vector3 direction = Input.GetAxisRaw("Vertical") * transform.forward +
                            Input.GetAxisRaw("Horizontal") * transform.right;
    #else
        Vector3 direction = Input.GetAxisRaw("Vertical") * Vector3.forward +
                            Input.GetAxisRaw("Horizontal") * Vector3.right;
    #endif

        // Slows down the character when not actively moving.
        if(direction.sqrMagnitude == 0.0f)
            _velocity *= 1.0f - Deacceleration * Time.deltaTime;
        // Slows down the character when over target speed.
        else if(speedDifference > 0.0f)
            _velocity *= 1.0f - (Deacceleration * Time.deltaTime * speedDifference / GetSpeed());
        
        direction.Normalize();
        _velocity += direction * (Acceleration * Time.deltaTime);
    }

    void CalculateRotation()
    {
        float x = Input.GetAxis("Mouse X"), // Left to Right
              y = -Input.GetAxis("Mouse Y"); // Down to Up

        _rotationX += x * MouseSensitivity;
        _rotationY = Mathf.Clamp(_rotationY + y * MouseSensitivity, RotationClamp.x, RotationClamp.y);
        
        transform.localRotation = Quaternion.Euler(0.0f, _rotationX, 0.0f);
        Camera.transform.localRotation = Quaternion.Euler(_rotationY, 0.0f, 0.0f);
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

        Debug.DrawRay(transform.position, Vector3.down, Color.yellow);
        RaycastHit rayResult;
        if (!Physics.Raycast(transform.position, Vector3.down, out rayResult, _controller.height))
            return result;

        result.Distance = rayResult.distance - _controller.skinWidth - _controller.height / 2.0f;
        result.Grounded = result.Distance < _controller.stepOffset || _controller.isGrounded;
        
        ISoundEmitter emitter = rayResult.collider.GetComponent<ISoundEmitter>();
        if (emitter != null)
            result.SoundSettings = emitter.GetSettings();
        
        return result;
    }
}
