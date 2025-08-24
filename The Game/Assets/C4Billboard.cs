using UnityEngine;

public class C4Billboard : MonoBehaviour
{
    public float BlinkTime = 0.5f;
    public float MinSize, MaxSize;
    public float DistanceMultipler;

    private float _time;
    private MeshRenderer _renderer;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!Camera.main)
            return;
        
        Vector3 cameraOffset = Camera.main.transform.position - transform.position;
        _time += Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(cameraOffset) * Quaternion.Euler(90, 0, 0);

        transform.localScale = Mathf.Clamp(cameraOffset.magnitude * DistanceMultipler, MinSize, MaxSize) * new Vector3(1, 1, 1);
        
        _renderer.material.SetFloat("_Opacity", 1f - Mathf.Clamp(_time % BlinkTime / BlinkTime, 0, 1));
    }
}
