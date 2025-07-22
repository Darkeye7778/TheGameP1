using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ConnectionProfile : MonoBehaviour
{
    public Connection Connection;
    public bool Connected = false;
    public bool Generated = false;
    public bool AlwaysOpen = false;

    public ConnectionProfile Other;
    public LayerMask layer;
    
    private void Awake()
    {
        MapGenerator.Instance.Parameters.Connections.Add(this);
    }

    public void Generate()
    {
        Collider collider = GetComponent<Collider>();
        collider.enabled = false;
        Connected = Physics.Raycast(transform.position + transform.forward * -0.1f, transform.forward, out RaycastHit hit, 1f, layer);
        collider.enabled = true;
        
        if (!Connected && Connection.IsEntrance)
            return;
        
        if (!Connected)
        {
            Instantiate(Utils.PickRandom(MapGenerator.Instance.Type.ClosedDoors), transform);
            return;
        }

        Other = hit.collider.GetComponent<ConnectionProfile>();
        
        Other.Generated = true;
        Other.Connected = true;

        bool isEntrance = Connection.IsEntrance || Other.Connection.IsEntrance;
        if (!isEntrance && Random.Range(0f, 1f) > MapGenerator.Instance.Type.ConnectRoomsOdds)
        {
            Other.Connected = false;
            return;
        }
        
        bool isDoor = Connection.HasDoor || Other.Connection.HasDoor;
        if (!Generated && isDoor)
            Instantiate(Utils.PickRandom(MapGenerator.Instance.Type.OpenDoors), transform);
    }

    private void Update()
    {
        Connected = Physics.Raycast(transform.position + transform.forward * -0.1f, transform.forward, out RaycastHit hit, 1f, layer);
        Debug.DrawRay(transform.position, transform.forward, Connected ? Color.green : Color.red);
    }
}
