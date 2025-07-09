using UnityEngine;
using System.Collections;

public class Claymore : MonoBehaviour, IDamagable
{
    [SerializeField] int tripLength = 4;
    [SerializeField] int explosionRadius = 5;
    [SerializeField] int explodeDelay = 2;
    bool activated = false;


    private void Update()
    {
        CheckForTrip();
    }

    void CheckForTrip()
    {
        if (activated) return;

        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward, out hit, tripLength))
        {
            if (hit.collider.CompareTag("Player"))
            {
                //Activate
                StartCoroutine(Activate());
            }
        }
    }

    IEnumerator Activate()
    {
        activated = true;
        Debug.Log("Claymore Activated");
        yield return new WaitForSeconds(explodeDelay);
        Debug.Log("Claymore Exploded");
        Explode();
        
    }

    void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider col in hits)
        {

            IDamagable damagable = col.GetComponent<IDamagable>();

            float damage = Mathf.Clamp(100f / (col.transform.position - transform.position).magnitude, 0f, 100f);
            if (damagable != null)
            {
                damagable.OnTakeDamage(new DamageSource { Name = "Claymore", Object = gameObject }, damage);
                Debug.Log(col.name + " Was Hit");
            }
            

            
        }
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        // This method is not used in this script, but is required by the IDamagable interface.
        Debug.Log($"Claymore has taken {damage} damage.");
    }
}