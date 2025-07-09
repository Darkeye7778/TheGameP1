using UnityEngine;
using System.Collections;

public class Claymore : MonoBehaviour
{
    public float detectionDistance = 5f;
    public float explosionDelay = 3f; // Delay before the claymore activates
    public bool shouldDebug = false;
    public LayerMask playerLayer;
    public AudioSource activationSound;
    public AudioSource explosionSound;


    DamageSource source = new DamageSource{};

     
   
    bool isActivated = false;
    private void Start()
    {
        source.Object = gameObject; // Set the source object to this claymore
        source.Name = "Claymore"; // Set the name of the source
    }
    void Update()
    {
        if (isActivated)
            return;

        if (CheckForPlayer()) StartCoroutine(Activate());

        if (shouldDebug)
        {
            Debug.DrawRay(transform.position, transform.forward * detectionDistance, Color.red);
        }
    }

    IEnumerator Activate()
    {
        isActivated = true;
        activationSound.Play();
        Debug.Log("Claymore activated! Waiting for explosion...");
        yield return new WaitForSeconds(explosionDelay);
        explosionSound.Play();
        Debug.Log("Claymore exploded!");
        
        Explode();

    }

    bool CheckForPlayer()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, playerLayer))
        {
            
            return true;
        }
        return false;
    }

    void Explode()
    {
        RaycastHit hit;

        if (Physics.SphereCast(transform.position, 5f, Vector3.up, out hit,playerLayer))
        {
            IDamagable damagable = hit.collider.GetComponent<IDamagable>();
            if (damagable != null)
            {
                damagable.OnTakeDamage(source, 100f / hit.distance); // Deal damage based on distance
                Debug.Log($"Dealt damage to {hit.collider.name} from Claymore explosion.");
            }
        }


        Destroy(gameObject);
    }
}