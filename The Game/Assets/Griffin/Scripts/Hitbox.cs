using UnityEngine;
using UnityEngine.Serialization;

public class Hitbox : MaterialProfile, IDamagable
{
    [field: SerializeField] public GameObject BaseObject { get; private set; }
    public IDamagable BaseDamagable { get; private set; }
    public MaterialProfile BaseMaterialProfile { get; private set; }
    public float Multiplier = 1f;

    private void Start()
    {
        BaseDamagable = BaseObject.GetComponent<IDamagable>();
        BaseMaterialProfile = BaseObject.GetComponent<MaterialProfile>();
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        BaseDamagable.OnTakeDamage(source, damage * Multiplier);
    }

    public GameObject GameObject()
    {
        return BaseDamagable.GameObject();
    }

    public Vector3[] AimTargets()
    {
        return BaseDamagable.AimTargets();
    }

    public Vector3 LookTarget()
    {
        return BaseDamagable.LookTarget();
    }

    public override MaterialSettings GetSettings()
    {
        return base.GetSettings() ?? BaseMaterialProfile?.GetSettings();
    }

    public IDamagable Base()
    {
        return BaseDamagable?.Base() ?? this;
    }

    public bool IsDead()
    {
        return BaseDamagable.IsDead();
    }
}
