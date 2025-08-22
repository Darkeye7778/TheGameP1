using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Decal : MonoBehaviour
{
    public float LifeTime = 1f;

    private float _life;
    private DecalProjector _projector;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _projector = GetComponent<DecalProjector>();
        
        _life = LifeTime;
    }

    // Update is called once per frame
    void Update()
    {
        _life -= Time.deltaTime;

        _projector.fadeFactor = _life / LifeTime;
        
        if(_life < 0) 
            Destroy(gameObject);
    }
}
