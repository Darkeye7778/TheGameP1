using System.Collections;
using UnityEngine;

public class HostageAI : MonoBehaviour, IDamagable
{
    [SerializeField] Renderer model;
    [SerializeField] float HP;

    Color colorOrig;

    public bool playerInTrigger;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager.instance.updateGameGoal(1);
        colorOrig = model.material.color;
    }

    // Update is called once per frame
    void Update()
    {
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
}
