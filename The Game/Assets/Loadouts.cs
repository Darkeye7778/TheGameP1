using UnityEngine;

[CreateAssetMenu(fileName = "New Loadout", menuName = "Scriptable Objects/Loadout")]
public class Loadout : ScriptableObject
{
    public Weapon Primary;
    public Weapon Secondary;
    public float Health;
    public float Stamina;
    public float SpeedMult;
}

