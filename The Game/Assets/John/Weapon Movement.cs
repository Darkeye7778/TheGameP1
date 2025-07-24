using UnityEngine;

public class WeaponMovement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public PlayerController pc;
    public PlayerInventory Inventory;
    [Header("Weapon Movement Settings")]
    public float LookSwayIntensity = 0.5f;
    public float MoveSwayIntensity = 0.5f;
    [Header("Weapon Rotation Limits")]
    public float zMaxAngle = 30f;
    public float xMaxAngle = 30f;
    public float yMaxAngle = 30f;
    public float rotSpeed = 5f;
    public float zeroSpeed = 1f;


    private Quaternion _currentRotation;
    Vector3 targetRot;
    

    // Update is called once per frame
    void Update()
    {
        if (float.IsNaN(targetRot.x)) targetRot.x = 0;
        if (float.IsNaN(targetRot.y)) targetRot.y = 0;
        if (float.IsNaN(targetRot.z)) targetRot.z = 0;
        
        // Smoothly interpolate the weapon's rotation towards the target rotation
        _currentRotation = Quaternion.Lerp(_currentRotation, Quaternion.Euler(targetRot), Time.deltaTime * rotSpeed);
        targetRot = Vector3.Lerp(targetRot, Vector3.zero, Time.deltaTime * zeroSpeed);
        Sway();

        transform.localRotation = _currentRotation * Inventory.CurrentWeapon.Weapon.Rotation;
    }

    void Sway()
    {
        // Calculate the target rotation based on mouse input and player velocity
        targetRot += new Vector3(
            Input.GetAxis("Mouse Y"),
            -Input.GetAxis("Mouse X") ,
            0f
        ) * LookSwayIntensity;

        targetRot += new Vector3(
            pc.LocalRealVelocity.y + -pc.LocalRealVelocity.z,
            0f,
            pc.LocalRealVelocity.x
        ) * MoveSwayIntensity;


        // Clamp the target rotation to prevent excessive rotation
        targetRot.z = Mathf.Clamp(targetRot.z, -zMaxAngle, zMaxAngle);
        targetRot.x = Mathf.Clamp(targetRot.x, -xMaxAngle, xMaxAngle);
        targetRot.y = Mathf.Clamp(targetRot.y, -yMaxAngle, yMaxAngle);
    }

    public void AddRecoil(float recoilIntensity)
    {
        targetRot += new Vector3(Random.Range(-recoilIntensity, 0), Random.Range(-recoilIntensity, recoilIntensity), Random.Range(-recoilIntensity, recoilIntensity));
    }

}
