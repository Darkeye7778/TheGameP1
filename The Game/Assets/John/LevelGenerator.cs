using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [SerializeField] GameObject[] levelPrefabs;
    public int numberOfsectors = 4; // Total number of levels to generate
    private void Start()
    {
        for (int i = 0; i < numberOfsectors / 2; i++)
        {
            for (int j = 0; j < numberOfsectors / 2; j++)
            {
                GameObject level = Instantiate(levelPrefabs[Random.Range(0, levelPrefabs.Length)]);
                level.transform.position = new Vector3(i * 10, 0, j * 10); // Adjust position for each level
                level.name = "Level " + (i + 1);
            }

        }
    }
}
