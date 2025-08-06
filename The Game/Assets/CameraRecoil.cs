using System;
using UnityEngine;

public class CameraRecoil : MonoBehaviour
{
    public bool IsShooting { private get; set; }

    [Range(1, 100)] public float SmoothingSpeed = 20;

    [Range(1, 100)] public float ResetSpeed = 10;

    Vector3 _targetRotation;


    private void Update()
    {
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(_targetRotation),
            Time.deltaTime * SmoothingSpeed);
        if (!IsShooting)
            _targetRotation =
            Vector3.Lerp(_targetRotation, Vector3.zero, Time.deltaTime * ResetSpeed);
    }

    public void AddRecoil(Weapon weapon)
    {
        Vector3 calcRecoil = new Vector3(-weapon.RecoilX, weapon.RecoilY, 0) * weapon.RecoilIntensity;
        _targetRotation += calcRecoil;
    }
}