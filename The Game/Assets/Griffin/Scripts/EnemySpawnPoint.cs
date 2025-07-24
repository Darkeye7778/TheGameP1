using System;
using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        MapGenerator.Instance.Parameters.EnemySpawnPoints.Add(this);
    }
    
    private void OnDestroy()
    {
        MapGenerator.Instance.Parameters.EnemySpawnPoints.Remove(this);
    }
}
