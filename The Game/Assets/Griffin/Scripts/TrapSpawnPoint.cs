using System;
using UnityEngine;

public class TrapSpawnPoint : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        MapGenerator.Instance.Parameters.TrapSpawnPoints.Add(this);
    }
    
    private void OnDestroy()
    {
        MapGenerator.Instance.Parameters.TrapSpawnPoints.Remove(this);
    }
}
