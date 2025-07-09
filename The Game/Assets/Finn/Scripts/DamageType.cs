using UnityEngine;
using System.Collections;

public class DamageType : MonoBehaviour
{
    enum damageType { moving, stationary, DOT, homing }
    [SerializeField] damageType type;
    [SerializeField] Rigidbody rb;

    [SerializeField] GameObject damageSource;

    [SerializeField] int damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int speed;
    [SerializeField] int destroyTime;

    bool isDamaging;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (type == damageType.moving || type == damageType.homing)
        {
            Destroy(gameObject, destroyTime);
            if (type == damageType.moving)
            {
                rb.linearVelocity = transform.forward * speed;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (type == damageType.homing)
        {
            rb.linearVelocity = (gameManager.instance.player.transform.position - transform.position).normalized * speed * Time.deltaTime;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;
        IDamagable dmg = other.GetComponent<IDamagable>();
        DamageSource source = new DamageSource();
        source.Name = damageSource.name;
        source.Object = damageSource;

        if (dmg != null && type != damageType.DOT)
        {
            dmg.OnTakeDamage(source, damageAmount);
        }
        if (type == damageType.moving || type == damageType.homing)
        {
            Destroy(gameObject);
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.isTrigger)
            return;
        IDamagable dmg = other.GetComponent<IDamagable>();

        if (dmg != null && type == damageType.DOT && !isDamaging)
        {
            StartCoroutine(damageOther(dmg));
        }
    }
    IEnumerator damageOther(IDamagable d)
    {
        DamageSource source = new DamageSource();
        source.Name = damageSource.name;
        source.Object = damageSource;
        isDamaging = true;
        d.OnTakeDamage(source, damageAmount);
        yield return new WaitForSeconds(damageRate);
        isDamaging = false;
    }
}
