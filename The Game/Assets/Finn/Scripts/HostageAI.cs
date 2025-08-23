using System.Collections;
using UnityEngine;

public class HostageAI : MonoBehaviour, IDamagable
{
    [SerializeField] Renderer model;
    [SerializeField] float HP;
    [SerializeField] private Transform Head;
    [SerializeField] private Sound Voicelines;
    [SerializeField] private float MinVoicelineInterval, MaxVoicelineInterval;
    private AudioSource _audioSource;

    private float _timer;

    Color colorOrig;

    public bool playerInTrigger;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        colorOrig = model.material.color;
        _audioSource = GetComponent<AudioSource>();
        _timer = Random.Range(MinVoicelineInterval, MaxVoicelineInterval);
    }

    // Update is called once per frame
    void Update()
    {
        _timer -= Time.deltaTime;

        if (_timer <= 0)
        {
            _timer = Random.Range(MinVoicelineInterval, MaxVoicelineInterval);
            _audioSource.PlayOneShot(Voicelines.PickSound(), Voicelines.Volume);
        }
            
        if (playerInTrigger)
        {
            Destroy(gameObject);
            gameManager.instance.updateHostagesSaved(1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;
        }
    }

    public void OnTakeDamage(DamageSource source, float amount)
    {
        Debug.Log($"{source} / {amount}");
        HP -= amount;
        if (HP <= 0)
        {
            Destroy(gameObject);
            gameManager.instance.youLose();
        }
        else
        {
            StartCoroutine(flashRed());
        }
    }

    public GameObject GameObject()
    {
        return gameObject;
    }

    public void OnDeath()
    {
        Destroy(gameObject);
        gameManager.instance.youLose();
    }

    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        model.material.color = colorOrig;
    }

    public Vector3[] AimTargets()
    {
        return new Vector3[1] { transform.position };
    }

    public Vector3 LookTarget()
    {
        return Head.position;
    }

    public bool IsDead()
    {
        return HP > 0;
    }
}
