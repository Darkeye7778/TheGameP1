using UnityEngine;

public static class TeamUtils
{
    public static bool IsFriendly(GameObject a, GameObject b)
    {
        if (a == null || b == null) return false;
        var ta = a.GetComponentInParent<Team>();
        var tb = b.GetComponentInParent<Team>();
        return ta && tb && ta.Faction == tb.Faction;
    }
}
