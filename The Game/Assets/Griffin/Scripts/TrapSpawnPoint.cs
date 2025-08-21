using System;
using UnityEngine;

public class TrapSpawnPoint : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        if (MapGenerator.Instance)
            MapGenerator.Instance.Parameters.TrapSpawnPoints.Add(this);
    }
    
    private void OnDestroy()
    {
        if (MapGenerator.Instance)
            MapGenerator.Instance.Parameters.TrapSpawnPoints.Remove(this);
    }
}
