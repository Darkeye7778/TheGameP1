using System;
using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        MapGenerator.Instance.Parameters.PlayerSpawnPoints.Add(this);
    }

    private void OnDestroy()
    {
        MapGenerator.Instance.Parameters.PlayerSpawnPoints.Remove(this);
    }
}
