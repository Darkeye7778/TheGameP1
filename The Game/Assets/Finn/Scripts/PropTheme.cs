using UnityEngine;

[CreateAssetMenu(menuName = "Props/Theme")]
public class PropTheme : ScriptableObject
{
    [System.Serializable]
    public class CategoryRule
    {
        public RoomCategory Category;
        public WeightedPrefab[] CenterSmall, CenterLarge, Corner, Wall, Ceiling;
        public int MinProps = -1, MaxProps = -1; // -1 means "use archetype defaults"
    }

    [System.Serializable]
    public class Rule
    {
        public RoomArchetype Archetype;

        // Fallback lists used if no category-specific override exists:
        public WeightedPrefab[] CenterSmall, CenterLarge, Corner, Wall, Ceiling;
        public int MinProps = 2, MaxProps = 6;

        // Optional category-specific overrides:
        public CategoryRule[] Categories;
    }

    public Rule[] Rules;

    public Rule GetRule(RoomArchetype a)
    {
        foreach (var r in Rules) if (r.Archetype == a) return r;
        return null;
    }
}