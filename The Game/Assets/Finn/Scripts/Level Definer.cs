using UnityEngine;

public class LevelDefiner : MonoBehaviour
{
    [SerializeField] public string LevelType;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        gameManager.instance.SetLevel(LevelType);
    }

}
