using UnityEngine;
using UnityEngine.Serialization;

public class Hitbox : SoundProfile, IDamagable
{
    [field: SerializeField] public GameObject BaseObject { get; private set; }
    public IDamagable BaseDamagable { get; private set; }
    public SoundProfile BaseSoundProfile { get; private set; }
    public float Multiplier = 1f;

    private void Start()
    {
        BaseDamagable = BaseObject.GetComponent<IDamagable>();
        BaseSoundProfile = BaseObject.GetComponent<SoundProfile>();
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

    public override SoundEmitterSettings GetSettings()
    {
        return base.GetSettings() ?? BaseSoundProfile?.GetSettings();
    }

    public IDamagable Base()
    {
        return BaseDamagable;
    }

    public bool IsDead()
    {
        return BaseDamagable.IsDead();
    }
}
