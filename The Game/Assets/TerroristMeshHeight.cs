using UnityEngine;
using UnityEngine.TextCore.Text;

public class TerroristMeshHeight : MonoBehaviour
{
    public GameObject Mesh;
    public CharacterController Controller;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Mesh.transform.localPosition = -Controller.height * 0.5f * Vector3.up;
    }
}
