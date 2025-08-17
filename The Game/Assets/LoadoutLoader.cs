using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class LoadoutLoader : MonoBehaviour
{
    public Loadout loadoutRef;
    public Image PrimaryImage;
    public Image SecondaryImage;
    public TMP_Text PrimaryName;
    public TMP_Text SecondaryName;
    public TMP_Text HealthText;
    public TMP_Text StaminaText;
    public TMP_Text SpeedText;

    
    public void Load(Loadout loadout)
    {
        loadoutRef = loadout;
        PrimaryImage.sprite = loadout.Primary.Image;
        SecondaryImage.sprite = loadout.Secondary.Image;
        PrimaryName.text = loadout.Primary.Name;
        SecondaryName.text = loadout.Secondary.Name;
        HealthText.text = ("Health: " + loadout.Health.ToString());
        StaminaText.text = ("Stamina: " + loadout.Stamina.ToString());
        SpeedText.text = ("SpeedMult: " + loadout.SpeedMult.ToString());
        
    }
}
