using TMPro;
using UnityEngine;

public class SimpleUIHandler : MonoBehaviour
{
    public PlayerInventory Inventory;

    public TextMeshProUGUI GunNameLabel, AmmoLabel, FireModeLabel; 
    
    void Start()
    {
            
    }
    
    void Update()
    {
        GunNameLabel.text = Inventory.CurrentWeapon.Weapon.Name;
        AmmoLabel.text = $"{Inventory.CurrentWeapon.LoadedAmmo}/{Inventory.CurrentWeapon.ReserveAmmo}";
        FireModeLabel.text = Inventory.CurrentWeapon.Mode.ToString();
    }
}
