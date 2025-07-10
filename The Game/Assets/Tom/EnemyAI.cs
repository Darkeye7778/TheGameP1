using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] Renderer Model;
    [SerializeField] int HP;

    Color OrigColor;
    void Start()
    {
        OrigColor = Model.material.color;
        gamemanager.instance.UpdateTerroristCount(1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(int Dmg)
    {
        HP -= Dmg;
        if (HP <= 0)
        {
            gamemanager.instance.UpdateTerroristCount(-1);
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(FlashRed());
        }
    }

    IEnumerator FlashRed()
    {
        Model.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        Model.material.color = OrigColor;
    }
}
