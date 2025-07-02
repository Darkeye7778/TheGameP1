#define PLAYERCONTROLLER_USE_INERTIA

using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour, IDamagable
{
    public GameObject Camera;
    
    [Range(0.01f, 10.0f)] public float Sensitivity;
    public float WalkingSpeed = 1.34f;
    public float RunningSpeed = 5.0f;
    public float Acceleration = 7.0f;
    public float Deacceleration = 15.0f;
    public Vector2 RotationClamp = new Vector2(-90.0f, 90.0f);

    private Vector3 _velocity;
    private float _rotationX, _rotationY;
    private CharacterController _controller;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
    }
    
    void Update()
    {
        CalculateVelocity();
        CalculateRotation();

    #if PLAYERCONTROLLER_USE_INERTIA
        _controller.Move(_velocity * Time.deltaTime);
    #else
        _controller.Move(transform.rotation * _velocity * Time.deltaTime);
    #endif
    }

    void CalculateVelocity()
    {
        bool grounded = _controller.isGrounded;
        float speedDifference = _velocity.sqrMagnitude - GetSpeed();

        if (grounded)
            _velocity.y = 0.0f;
        else
        {
            _velocity += Physics.gravity * Time.deltaTime;
            return;
        }

    #if PLAYERCONTROLLER_USE_INERTIA
        Vector3 direction = Input.GetAxisRaw("Vertical") * transform.forward +
                            Input.GetAxisRaw("Horizontal") * transform.right;
    #else
        Vector3 direction = Input.GetAxisRaw("Vertical") * Vector3.forward +
                            Input.GetAxisRaw("Horizontal") * Vector3.right;
    #endif
        direction.Normalize();

        // Slows down the character when not actively moving.
        if(direction.sqrMagnitude == 0.0f)
            _velocity *= 1.0f - Deacceleration * Time.deltaTime;
        // Slows down the character when over target speed.
        else if(speedDifference > 0.0f)
            _velocity *= 1.0f - (Deacceleration * Time.deltaTime * speedDifference / GetSpeed());
        
        _velocity += direction * (Acceleration * Time.deltaTime);
    }

    void CalculateRotation()
    {
        float x = Input.GetAxis("Mouse X"), // Left to Right
              y = -Input.GetAxis("Mouse Y"); // Down to Up

        _rotationX += x * Sensitivity;
        _rotationY = Mathf.Clamp(_rotationY + y * Sensitivity, RotationClamp.x, RotationClamp.y);
        
        transform.localRotation = Quaternion.Euler(0.0f, _rotationX, 0.0f);
        Camera.transform.localRotation = Quaternion.Euler(_rotationY, 0.0f, 0.0f);
    }

    float GetSpeed()
    {
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
}
