using UnityEngine;

public interface IDamagable
{
    public void OnTakeDamage(float damage);
    public void OnDeath();
}
