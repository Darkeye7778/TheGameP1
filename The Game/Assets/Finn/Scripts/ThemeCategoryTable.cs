using UnityEngine;

[CreateAssetMenu(menuName = "Props/Theme Category Table")]
public class ThemeCategoryTable : ScriptableObject
{
    [System.Serializable] public struct WeightedCategory
    {
        public RoomCategory Category;
        public float Weight;
    }

    [System.Serializable]
    public class ArchetypeSet
    {
        public RoomArchetype Archetype;
        public WeightedCategory[] Pool;
        public int minTotal = 0;
        public int maxTotal = 9999;
    }

    [System.Serializable]
    public class Quota
    {
        public RoomCategory Category;
        public int minTotal = 0;
        public int maxTotal = 9999;
    }

    public ArchetypeSet[] Sets;
    public Quota[] Quotas;

    public ArchetypeSet Get(RoomArchetype a)
    {
        foreach (var s in Sets) if (s.Archetype == a) return s;
        return null;
    }
}
