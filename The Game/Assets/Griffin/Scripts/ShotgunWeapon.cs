using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ShotgunWeapon", menuName = "Scriptable Objects/ShotgunWeapon")]
public class ShotgunWeapon : Weapon
{
    [Header("Shotgun Stats")]
    [Tooltip("Number of pellets")] public uint Pellets;
    [FormerlySerializedAs("Spread")] [Tooltip("Angle of cone for spread.")] [Range(0, 90)] public float SpreadAngle;

    public override IDamagable[] CastShot(Ray ray, LayerMask mask, GameObject inflicter)
    {
        float unitCircleRadius = Mathf.Sin(SpreadAngle * Mathf.Deg2Rad);
        Quaternion rayDirection = Quaternion.LookRotation(ray.direction);
        
        IDamagable[] result = new IDamagable[Pellets];
        for (uint i = 0; i < Pellets; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * unitCircleRadius;
            Vector3 pelletDirection = rayDirection * new Vector3(randomPoint.x, randomPoint.y, 1);

            result[i] = CastRay(new Ray(ray.origin, pelletDirection), mask, inflicter);
        }
        
        return result;
    }
}
