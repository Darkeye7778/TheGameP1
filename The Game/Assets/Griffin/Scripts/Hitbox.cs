using UnityEngine;

public class Hitbox : SoundProfile, IDamagable
{
    public GameObject Base;
    public float Multiplier = 1f;

    private IDamagable _baseDamagable;
    public SoundProfile _baseSoundProfile;

    private void Start()
    {
        _baseDamagable = Base.GetComponent<IDamagable>();
        _baseSoundProfile = Base.GetComponent<SoundProfile>();
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        _baseDamagable.OnTakeDamage(source, damage * Multiplier);
    }

    public GameObject GameObject()
    {
        return _baseDamagable.GameObject();
    }

    public override SoundEmitterSettings GetSettings()
    {
        return base.GetSettings() ?? _baseSoundProfile.GetSettings();
    }
}
