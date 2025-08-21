using System;
using UnityEngine;

public class WeaponRotationPivot : MonoBehaviour
{
    public float Rotation;
    public float MinVertical, MaxVertical;
    public float MinRotation, MaxRotation;
    
    public Quaternion FinalRotation { get; private set; }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float rotation = -Mathf.Clamp(Rotation, MinRotation, MaxRotation);
        float fac = -Mathf.Sin(rotation * Mathf.Deg2Rad) * 0.5f + 0.5f;
        Vector3 target = Mathf.Lerp(MinVertical, MaxVertical, fac) * Vector3.up;

        Transform parentTransform = transform.parent.transform;

        transform.position = parentTransform.position + target; 
        transform.rotation = FinalRotation = Quaternion.Euler(rotation, parentTransform.rotation.eulerAngles.y, 0);
    }
}
