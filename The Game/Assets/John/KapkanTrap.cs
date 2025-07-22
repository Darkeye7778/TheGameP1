
using System;
using System.Collections.Generic;
using UnityEngine;

public class Explosion
{
    private const uint MAX_EXPLOSION_COLLIDER = 5;
    
    // Alternative inverse square law.
    public static float Falloff(float distance, float minDistance, float maxDistance)
    {
        return Mathf.Clamp01((-distance + minDistance) / (maxDistance - minDistance) + 1);
    }

    private static Collider[] GetObjectsInRadius_Colliders = new Collider[MAX_EXPLOSION_COLLIDER];
    public static List<IDamagable> GetObjectsInRadius(Vector3 position, float radius, LayerMask mask)
    {
        var size = Physics.OverlapSphereNonAlloc(position, radius, GetObjectsInRadius_Colliders, mask);

        List<IDamagable> damagables = new List<IDamagable>();
        for (uint i = 0; i < size; i++)
        {
            IDamagable dmg;
            if (GetObjectsInRadius_Colliders[i].TryGetComponent(out dmg))
                damagables.Add(dmg);
        }

        return damagables;
    }
};

public class KapkanTrap : MonoBehaviour, IDamagable
{
    public float DetectionDistance;
    public float ExplosionMinRadius, ExplosionMaxRadius;
    public float Damage;
    public float ExplosionDelay;
    public LayerMask DetectionLayer, DamageLayer;

    public AudioClip DetectionSound, ExplosionSound;

    private float _explosionTimer;
    private bool _detected;
    private AudioSource _audioSource;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!_detected)
        {
            _detected = Physics.Raycast(transform.position, transform.right, DetectionDistance, DetectionLayer);
        #if UNITY_EDITOR
            Debug.DrawRay(transform.position, transform.right * DetectionDistance, Color.red);
        #endif
            if (!_detected)
                return;
            
            _audioSource.PlayOneShot(DetectionSound);
        }

        _explosionTimer += Time.deltaTime;

        if (_explosionTimer < ExplosionDelay)
            return;
        
        List<IDamagable> inRadius = Explosion.GetObjectsInRadius(transform.position, ExplosionMaxRadius, DamageLayer);

        foreach (IDamagable dmg in inRadius)
        {
            float distance = Vector3.Distance(dmg.GameObject().transform.position, transform.position);
            float damage = Explosion.Falloff(distance, ExplosionMinRadius, ExplosionMaxRadius) * Damage;

            dmg.OnTakeDamage(new DamageSource("Explosive Trap", gameObject), damage);
        }
        
        AudioSource.PlayClipAtPoint(ExplosionSound, transform.position);
        
        Destroy(gameObject);
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        Destroy(gameObject);
    }

    public GameObject GameObject()
    {
        return gameObject;
    }
}
