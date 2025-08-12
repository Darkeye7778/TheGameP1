using UnityEngine;

public class PlayerInventory : Inventory
{ 
    void Update()
    {
        if (Time.timeScale == 0.0f)
            return;

        InputFlags = 0;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            InputFlags |= InputState.UsePrimary;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            InputFlags |= InputState.UseSecondary;

        if (Input.GetKeyDown(KeyCode.B))
            InputFlags |= InputState.CycleFireMode;

        if (Input.GetButton("Fire1"))
            InputFlags |= InputState.Firing;
        
        if (Input.GetButtonDown("Fire1"))
            InputFlags |= InputState.FiringFirst;

        if (Input.GetKeyDown(KeyCode.R))
            InputFlags |= InputState.Reload;
        
        base.Update();
    }
}
