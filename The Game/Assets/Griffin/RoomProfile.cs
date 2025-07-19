using Unity.VisualScripting;
using UnityEngine;

public class RoomProfile : MonoBehaviour
{ 
    [field: SerializeField]
    public RoomProperties Properties { get; private set; }

    public bool GenerateLeafs(uint connectionIndex, GridTransform exitOffset, ref GenerationParams parameters)
    {
        var connectionIn = Properties.ConnectionPoints[connectionIndex];
        transform.localRotation = Direction.ToQuaternion(Direction.RotateToSouth(connectionIn.Transform.Rotation));
        transform.localPosition = exitOffset.WorldPosition - Quaternion.Inverse(transform.localRotation) * connectionIn.Transform.WorldPosition;
        
        bool generatedAll = true;

        parameters.RemainingRooms--;

        for (var i = 0; i < Properties.ConnectionPoints.Length; i++)
        {
            var connection = Properties.ConnectionPoints[i];
            
            if(i == connectionIndex)
                continue;

            float rand = Random.Range(0.0f, 1.0f);
            if (rand > 0.75f)
                continue;

            if (parameters.RemainingRooms == 0)
                break;

            GameObject newCell = Instantiate(MapGenerator.Instance.PickRandomCell().Prefab, transform);
            newCell.GetComponent<RoomProfile>().GenerateLeafs(0, connection.Transform, ref parameters);
        }

        return generatedAll;
    }
}
