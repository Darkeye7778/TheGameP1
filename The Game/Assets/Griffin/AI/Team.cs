using UnityEngine;

public enum Faction { Player, Enemy, Neutral }

public class Team : MonoBehaviour
{
    public Faction Faction = Faction.Enemy; // Set this on your prefabs
}