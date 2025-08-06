using TMPro;
using UnityEngine;

public class SimpleUIHandler : MonoBehaviour
{
    public PlayerController Controller;
    public PlayerInventory Inventory;

    public TextMeshProUGUI GunNameLabel, AmmoLabel, FireModeLabel, StaminaLabel; 
    
    void Start()
    {
        
    }
    
    void Update()
    {
        GunNameLabel.text = Inventory.CurrentWeapon.Weapon.Name;
        AmmoLabel.text = $"{Inventory.CurrentWeapon.LoadedAmmo}/{Inventory.CurrentWeapon.ReserveAmmo}";
        FireModeLabel.text = Inventory.CurrentWeapon.Mode.ToString();
        StaminaLabel.text = Controller.StaminaRelative.ToString();
    }
}
