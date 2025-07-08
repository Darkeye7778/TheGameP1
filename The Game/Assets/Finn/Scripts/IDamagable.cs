using UnityEngine;

public struct DamageSource
{
    public string Name;
    public GameObject Object;
}

public interface IDamagable
{
    public void OnTakeDamage(DamageSource source, float damage);
}
