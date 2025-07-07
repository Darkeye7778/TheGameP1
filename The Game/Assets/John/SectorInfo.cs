using UnityEngine;

public class SectorInfo : MonoBehaviour
{
    public Transform[] exits;

    public Transform entrance;

    public Vector3 direction;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Sector"))
        {
            Destroy(other.gameObject);
        }
    }
}
