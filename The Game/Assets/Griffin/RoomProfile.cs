//#define ROOM_KEEP_PARENT

using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoomProfile : MonoBehaviour
{ 
    [field: SerializeField]
    public RoomProperties Properties { get; private set; }

    public bool AlreadyGenerated { get; private set; } = false;

    private void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        
        collider.size = Properties.CollisionBox;
        collider.center = Properties.CollisionOffset;
    }

    public bool CheckCollision()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        collider.enabled = false;
        bool hit = Physics.CheckBox(transform.position + transform.rotation * Properties.CollisionOffset, Properties.CollisionBox * 0.45f);
        collider.enabled = true;
        
        return hit;
    }

    public bool TryFit()
    {
        bool didFit = !CheckCollision();
        
        if(!didFit)
            Destroy(gameObject);
        else
        {
            MapGenerator.Instance.Parameters.Rooms.Add(this);
            
            if(Properties.Type != RoomType.Hallway)
                MapGenerator.Instance.Parameters.RemainingRooms--;
        }

        return didFit;
    }

    public void GenerateLeafs()
    {
        AlreadyGenerated = true;

        foreach (Connection connection in Properties.ConnectionPoints)
        {

            if (connection.Required || Random.Range(0f, 1f) < connection.Odds || MapGenerator.Instance.Parameters.RemainingRooms > 0)
            {
                Vector3 position = transform.position + transform.rotation * connection.Transform.WorldPosition;
                Quaternion rotation = transform.rotation * Direction.ToQuaternion(connection.Transform.Rotation);

                for (uint i = 0; i < MapGenerator.Instance.MaxLeafRetry; i++)
                {
                    GameObject leaf = Instantiate(MapGenerator.Instance.PickRandomCell().Prefab, position, rotation);
                    if (leaf.GetComponent<RoomProfile>().TryFit()) // If it fits, let it be. Otherwise, try again.
                        break;
                }
            }
        }
    }

    private void Update()
    {
        bool collision = CheckCollision();
        Debug.DrawRay(transform.position, transform.forward * 1f, collision ? Color.red :  Color.green);
    }
}
