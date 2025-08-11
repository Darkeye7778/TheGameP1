using UnityEngine;

public class WeaponMovement : MonoBehaviour
{
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

    private Quaternion _currentRotation = Quaternion.identity;   // <-- init to a valid rotation
    private Vector3 targetRot;

    void Awake()
    {
        // Start from whatever the prefab currently has, but keep it valid
        if (IsInvalid(transform.localRotation))
            transform.localRotation = Quaternion.identity;

        _currentRotation = transform.localRotation;
    }

    void Update()
    {
        if (Inventory == null || Inventory.CurrentWeapon == null || Inventory.CurrentWeapon.Weapon == null)
            return;

        // keep targetRot finite
        if (float.IsNaN(targetRot.x)) targetRot.x = 0;
        if (float.IsNaN(targetRot.y)) targetRot.y = 0;
        if (float.IsNaN(targetRot.z)) targetRot.z = 0;

        // Smooth rotate towards sway target
        var targetQ = Quaternion.Euler(targetRot);
        _currentRotation = Quaternion.Slerp(_currentRotation, targetQ, Time.deltaTime * rotSpeed);
        targetRot = Vector3.Lerp(targetRot, Vector3.zero, Time.deltaTime * zeroSpeed);

        Sway();

        // Sanitize weapon rotation before applying
        var weaponRot = Safe(Inventory.CurrentWeapon.Weapon.Rotation);
        transform.localRotation = _currentRotation * weaponRot;
    }

    void Sway()
    {
        if (pc == null) return;

        targetRot += new Vector3(
            Input.GetAxis("Mouse Y"),
            -Input.GetAxis("Mouse X"),
            0f) * LookSwayIntensity;

        targetRot += new Vector3(
            pc.LocalRealVelocity.y + -pc.LocalRealVelocity.z,
            0f,
            pc.LocalRealVelocity.x) * MoveSwayIntensity;

        targetRot.z = Mathf.Clamp(targetRot.z, -zMaxAngle, zMaxAngle);
        targetRot.x = Mathf.Clamp(targetRot.x, -xMaxAngle, xMaxAngle);
        targetRot.y = Mathf.Clamp(targetRot.y, -yMaxAngle, yMaxAngle);
    }

    public void AddRecoil(float recoilIntensity)
    {
        targetRot += new Vector3(
            Random.Range(-recoilIntensity, 0),
            Random.Range(-recoilIntensity, recoilIntensity),
            Random.Range(-recoilIntensity, recoilIntensity));
    }

    // ---- helpers ----
    static bool IsInvalid(Quaternion q)
    {
        return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) ||
               (Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z) + Mathf.Abs(q.w)) < 1e-6f; // zero quaternion
    }
    static Quaternion Safe(Quaternion q) => IsInvalid(q) ? Quaternion.identity : q;
}
  