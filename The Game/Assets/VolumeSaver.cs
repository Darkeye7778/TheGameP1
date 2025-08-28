using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class VolumeSaver : MonoBehaviour
{
    public Scrollbar slider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slider.value = PlayerPrefs.GetFloat("VolumeSaver");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
