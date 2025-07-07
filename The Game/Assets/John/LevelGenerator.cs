using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public GameObject startRoom;
    public GameObject[] rooms;

    GameObject curRoom;
    SectorInfo sectorInfo;
    Vector3 nextPos;


    private void Start()
    {
       GenerateLevel();
    }

    public void GenerateLevel()
    {
        GenerateStart();

        //hallways
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                curRoom = Instantiate(rooms[Random.Range(0, rooms.Length)]);
                sectorInfo = curRoom.GetComponent<SectorInfo>();
                curRoom.transform.position = nextPos;
                nextPos = sectorInfo.exits[Random.Range(0, sectorInfo.exits.Length)].position;


            }
        }
    }

    void GenerateStart()
    {
        curRoom = Instantiate(startRoom);
        curRoom.transform.position = transform.position;
        sectorInfo = curRoom.GetComponent<SectorInfo>();
        nextPos = sectorInfo.exits[Random.Range(0, sectorInfo.exits.Length)].position;
    }
}
