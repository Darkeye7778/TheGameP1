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
        if (Generated) return;

        var col = GetComponent<Collider>();
        bool prevEnabled = col ? col.enabled : true;
        if (col) col.enabled = false;
        
        int mask = MapGenerator.Instance.ExitMask;
        Vector3 origin = transform.position - transform.forward * 0.02f;
        float radius = 0.08f;

        Connected = false;
        Other = null;

        var hits = Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var cp = hits[i].GetComponent<ConnectionProfile>();
                if (!cp || cp == this) continue;

                float facing = Vector3.Dot(transform.forward, -cp.transform.forward);
                if (facing < 0.85f) continue;

                float d = (cp.transform.position - origin).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    Other = cp;
                }
            }

            if (Other) Connected = true;
        }

        if (!Connected)
        {
            if (Physics.Raycast(origin, transform.forward, out var hit, 1.5f, mask, QueryTriggerInteraction.Ignore))
            {
                var cp = hit.collider.GetComponent<ConnectionProfile>();
                if (cp && cp != this)
                {
                    float facing = Vector3.Dot(transform.forward, -cp.transform.forward);
                    if (facing >= 0.85f)
                    {
                        Other = cp;
                        Connected = true;
                    }
                }
            }
        }

        if (col) col.enabled = prevEnabled;

        if (Connected)
        {
            if (!Other || !Other.gameObject)
            {
                Connected = false;
                Other = null;
            }
            else
            {
                Other.Generated = true;
                Other.Connected = true;
                Other.Other = this;
            }
        }

        if (!Connected && Connection.IsEntrance)
            return;

        if (!Connected)
        {
            TrySpawnRandom(MapGenerator.Instance.Type.ClosedDoors, transform);
            return;
        }

        bool keep =
            Connection.IsEntrance || (Other != null && Other.Connection.IsEntrance) ||
            Connection.Required || (Other != null && Other.Connection.Required);

        if (!keep && Random.Range(0f, 1f) > MapGenerator.Instance.Type.ConnectRoomsOdds)
        {
            Connected = false;
            if (Other != null) Other.Connected = false;
            TrySpawnRandom(MapGenerator.Instance.Type.ClosedDoors, transform);
            return;
        }

        bool wantsDoor = Connection.HasDoor || (Other != null && Other.Connection.HasDoor);
        if (wantsDoor && !Generated)
            TrySpawnRandom(MapGenerator.Instance.Type.OpenDoors, transform);

        Generated = true;
    }


    private void Update()
    {
        Debug.DrawRay(transform.position, transform.forward, Connected ? Color.green : Color.red);
    }
}
