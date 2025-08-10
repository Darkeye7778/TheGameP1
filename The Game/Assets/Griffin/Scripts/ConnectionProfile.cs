using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ConnectionProfile : MonoBehaviour
{
    public Connection Connection;
    public bool Connected = false;
    public bool Generated = false;

    public ConnectionProfile Other;
    
    private void Awake()
    {
        MapGenerator.Instance.Parameters.Connections.Add(this);
        Generated = false;
    }

    private void TrySpawnRandom(GameObject[] prefabs, Transform parent)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        Instantiate(Utils.PickRandom(prefabs), parent);
    }

    public void Generate()
    {
        if (!Generated)
        {
            Collider collider = GetComponent<Collider>();
            collider.enabled = false;
            Connected = Physics.Raycast(
                transform.position + transform.forward * -0.1f,
                transform.forward, out RaycastHit hit, 1f,
                MapGenerator.Instance.ExitMask);
            collider.enabled = true;

            if (Connected)
            {
                Other = hit.collider.GetComponent<ConnectionProfile>();
                Other.Generated = true;
                Other.Connected = true;
                Other.Other = this;
            }
        }

        if (!Connected && Connection.IsEntrance)
            return;

        if (!Connected)
        {
            // was: Instantiate(Utils.PickRandom(MapGenerator.Instance.Type.ClosedDoors), transform);
            TrySpawnRandom(MapGenerator.Instance.Type.ClosedDoors, transform);
            return;
        }

        bool isEntrance = Connection.IsEntrance || Other.Connection.IsEntrance;
        if (!isEntrance && UnityEngine.Random.Range(0f, 1f) > MapGenerator.Instance.Type.ConnectRoomsOdds)
        {
            Connected = false;
            Other.Connected = false;
            TrySpawnRandom(MapGenerator.Instance.Type.ClosedDoors, transform);
            return;
        }

        bool isDoor = Connection.HasDoor || Other.Connection.HasDoor;
        if (!Generated && isDoor)
        {
            // was: Instantiate(Utils.PickRandom(MapGenerator.Instance.Type.OpenDoors), transform);
            TrySpawnRandom(MapGenerator.Instance.Type.OpenDoors, transform);
        }
    }

    private void Update()
    {
        Debug.DrawRay(transform.position, transform.forward, Connected ? Color.green : Color.red);
    }
}
