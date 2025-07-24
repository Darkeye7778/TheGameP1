using UnityEngine;
using System.Collections;

public class DamageType : MonoBehaviour
{
    public struct PoisonData 
    {
        public float damagePerTick;
        public float tickRate;
        public int duration;
        public PoisonData(float damage, float rate, int statusDuration)
        {
            damagePerTick = damage;
            tickRate = rate;
            duration = statusDuration;
        }
    }
    enum damageType { moving, stationary, DOT, homing, poison }
    [SerializeField] damageType type;
    [SerializeField] Rigidbody rb;

    [SerializeField] GameObject damageSource;

    [SerializeField] float initialDamage;
    [SerializeField] float damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int speed;
    [SerializeField] int statusDuration;
    [SerializeField] int destroyTime;

    bool isDamaging;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (type == damageType.moving || type == damageType.homing || type == damageType.poison)
        {
            Destroy(gameObject, destroyTime);

            if (type == damageType.moving || type == damageType.poison)
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
            Vector3 direction = (gameManager.instance.player.transform.position - transform.position).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, direction * speed, 0.1f);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;
        IDamagable dmg = other.GetComponent<IDamagable>();

        if (dmg != null)
        {
            DamageSource source = new DamageSource
            {
                Name = gameObject.name,
                Object = gameObject
            };

            if (type == damageType.poison)
            {
                dmg.OnTakeDamage(source, initialDamage);
                var poison = new PoisonData(damageAmount, damageRate, destroyTime);
                other.SendMessage("ApplyPoison", poison, SendMessageOptions.DontRequireReceiver);
            }
            else if (type != damageType.DOT)
            {
                dmg.OnTakeDamage(source, damageAmount);
            }
        }

        if (type == damageType.moving || type == damageType.homing || type == damageType.poison)
        {
            Destroy(gameObject);
        }
        //if (dmg != null && type != damageType.DOT)
        //{
        //    dmg.OnTakeDamage(source, damageAmount);
        //}
        //if (type == damageType.moving || type == damageType.homing)
        //{
        //    Destroy(gameObject);
        //}
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
