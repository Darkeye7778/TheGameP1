using UnityEngine;

public class PlayerFreezeToggle : MonoBehaviour
{
    [Header("Player scripts to disable when frozen")]
    [SerializeField] private MonoBehaviour[] componentsToDisable; // e.g., PlayerController, MouseLook, etc.

    [Header("Optional: clear weapon inputs while frozen")]
    [SerializeField] private Inventory inventory; // if present, clears shoot/reload flags

    public void SetFrozen(bool frozen)
    {
        if (componentsToDisable != null)
        {
            foreach (var c in componentsToDisable)
                if (c) c.enabled = !frozen;
        }

        if (inventory != null)
        {
            // Zero any queued input flags so nothing fires on resume
            var flagsField = typeof(Inventory).GetField("InputFlags",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (flagsField != null) flagsField.SetValue(inventory, 0);
        }
    }
}
