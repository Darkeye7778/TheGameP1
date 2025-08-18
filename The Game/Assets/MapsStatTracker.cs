using TMPro;
using UnityEngine;

public class MapsStatTracker : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public TextMeshPro MapStatsText;
    public string PlayerPrefKey;
    public int minimumSaved;
    void Start()
    {
        int saved = PlayerPrefs.GetInt("TotalHostagesSaved");
        int mapSaved = PlayerPrefs.GetInt(PlayerPrefKey);

        if (saved < minimumSaved)
        {
            MapStatsText.text = "Save " + minimumSaved.ToString() + " to access";
        }
        else
        {
            MapStatsText.text = "Hostages Saved: " + mapSaved.ToString();
        }
        
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayerPrefs.SetInt(PlayerPrefKey, PlayerPrefs.GetInt(PlayerPrefKey) + 1);
        }
    }
    
        
}
