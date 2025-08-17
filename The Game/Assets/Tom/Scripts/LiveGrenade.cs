using UnityEngine;
using System.Collections;
public class Grenade : MonoBehaviour
{
    [SerializeField] Rigidbody rb;
    [SerializeField] GameObject Explosion;
    [SerializeField] GameObject ExplosionEffect;
    [SerializeField] float ExplodeTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(Explode());

    }

    IEnumerator Explode()
    {
        yield return new WaitForSeconds(ExplodeTime);
        Instantiate(Explosion, transform.position, Quaternion.identity);
        Instantiate(ExplosionEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
