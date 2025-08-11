using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraRecoil : MonoBehaviour
{
    public bool IsShooting { private get; set; }

    [Range(1, 100)] public float SmoothingSpeed = 20;

    [Range(1, 100)] public float ResetSpeed = 10;

    Vector3 _targetRotation;
    
    float _timeSinceLastShot;

    float _resetDelay = 0.1f;

    private void Update()
    {
        _timeSinceLastShot += Time.deltaTime;
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(_targetRotation),
            Time.deltaTime * SmoothingSpeed);
        
        if (_timeSinceLastShot >= _resetDelay)
            _targetRotation =
            Vector3.Lerp(_targetRotation, Vector3.zero, Time.deltaTime * ResetSpeed);
    }

    public void AddRecoil(Weapon weapon)
    {
        Vector3 calcRecoil = new Vector3(-weapon.RecoilX, weapon.RecoilY + Random.Range(-0.1f,0.1f), 0) * weapon.RecoilIntensity;
        _targetRotation += calcRecoil;
        _timeSinceLastShot = 0;
    }
}