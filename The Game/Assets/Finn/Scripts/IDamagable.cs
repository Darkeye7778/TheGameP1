using UnityEngine;

public struct DamageSource
{
    public DamageSource(string name, GameObject obj)
    {
        Name = name;
        Object = obj;
    }
    
    public string Name;
    public GameObject Object;
}


public interface IDamagable
{
    public void OnTakeDamage(DamageSource source, float damage);
    public GameObject GameObject();
    public Vector3[] AimTargets();
    public Vector3 AimTarget() { return AimTargets()[0]; }
    public Vector3 LookTarget();
    public bool IsDead();
    public IDamagable Base() { return this; }
}
