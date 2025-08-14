using UnityEngine;

[CreateAssetMenu(fileName = "LevelDefinition", menuName = "Scriptable Objects/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    public LevelClass Level;
    public MapType MapType;              // Which tileset/rooms/doors/player prefab to use
    public PropTheme Theme;
    public ThemeCategoryTable CategoryTable;

    [Header("Generation Overrides")]
    public uint TargetRooms = 10;
    public int EnemySpawnAmount = 4;
    public int TrapSpawnAmount = 4;
    public int HostageSpawnAmount = 2;

    [Header("Seeding")]
    public bool UseFixedSeed = false;
    public int FixedSeed = 12345;
    public bool UseFixedSpawnSeed = false;
    public int FixedSpawnSeed = 67890;

    [Header("Timer (optional)")]
    public float StartingTime = 120f; 
}
