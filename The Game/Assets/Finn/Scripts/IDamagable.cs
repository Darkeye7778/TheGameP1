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
}
