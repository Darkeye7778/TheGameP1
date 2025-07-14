using UnityEngine;

public class WeaponMovement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public PlayerController pc;
    [Header("Weapon Movement Settings")]
    public float swayIntensity = 0.5f;
    [Header("Weapon Rotation Limits")]
    public float zMaxAngle = 30f;
    public float xMaxAngle = 30f;
    public float yMaxAngle = 30f;
    public float rotSpeed = 5f;
    public float zeroSpeed = 1f;
    [Header("Recoil Settings")]
    public float recoilIntensity = 1.5f;
    

    
    Vector3 targetRot;
    

    // Update is called once per frame
    void Update()
    {
        //handle recoil input for testing purposes
        if (Input.GetKeyDown(KeyCode.P))
        {
            AddRecoil(new Vector3(Random.Range(-recoilIntensity, 0), Random.Range(-recoilIntensity, recoilIntensity), Random.Range(-recoilIntensity, recoilIntensity)));
        }
        // Smoothly interpolate the weapon's rotation towards the target rotation
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(targetRot), Time.deltaTime * rotSpeed);
        targetRot = Vector3.Lerp(targetRot, Vector3.zero, Time.deltaTime * zeroSpeed);
        Sway();
    }

    void Sway()
    {
        // Calculate the target rotation based on mouse input and player velocity
        targetRot += new Vector3(
            -Input.GetAxis("Mouse Y") * swayIntensity,
            Input.GetAxis("Mouse X") * swayIntensity,
            -pc.RealVelocity.normalized.x * swayIntensity
        );


        // Clamp the target rotation to prevent excessive rotation
        targetRot.z = Mathf.Clamp(targetRot.z, -zMaxAngle, zMaxAngle);
        targetRot.x = Mathf.Clamp(targetRot.x, -xMaxAngle, xMaxAngle);
        targetRot.y = Mathf.Clamp(targetRot.y, -yMaxAngle, yMaxAngle);
    }

    public void AddRecoil(Vector3 recoilRot)
    {
        // Add recoil to the target rotation
        targetRot.x += recoilRot.x;
        targetRot.y += recoilRot.y;
        targetRot.z += recoilRot.z;
        targetRot.z = Mathf.Clamp(targetRot.z, -zMaxAngle, zMaxAngle);
    }


}
