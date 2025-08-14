using UnityEngine;

public enum SocketType { CenterSmall, CenterLarge, Corner, Wall, Ceiling }

public class PropSocket : MonoBehaviour
{
    public SocketType Type = SocketType.Wall;
    [Tooltip("Clear radius around socket (m).")] public float Clearance = 0.35f;
    [Tooltip("Facing hint; +Z is 'out from wall'.")] public Vector3 ForwardHint = Vector3.forward;
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, Clearance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.rotation * Vector3.forward * 0.4f);
    }

}
