using System;
using UnityEngine;

public class HostageSpawnPoint : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        MapGenerator.Instance.Parameters.HostageSpawnPoints.Add(this);
    }
    
    private void OnDestroy()
    {
        MapGenerator.Instance.Parameters.HostageSpawnPoints.Remove(this);
    }
}
