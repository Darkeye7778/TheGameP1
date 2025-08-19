using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Catalog")]
    [SerializeField] private List<LevelDefinition> levels;

    private Dictionary<LevelClass, LevelDefinition> map;

    private bool _loading;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        map = new Dictionary<LevelClass, LevelDefinition>(levels.Count);
        foreach (var def in levels)
            if (!map.ContainsKey(def.Level)) map.Add(def.Level, def);
    }

    IEnumerator Start()
    {
        // Start the game in the hub (police station)
        yield return LoadLevelRoutine(LevelClass.Hub);
    }

    public void LoadLevel(LevelClass level)
    {
        if (_loading) return;
        StartCoroutine(LoadLevelRoutine(level));
    }

    private IEnumerator LoadLevelRoutine(LevelClass level)
    {
        _loading = true;
        gameManager.instance.statePause();

        if (!map.TryGetValue(level, out var def))
        {
            Debug.LogError($"No LevelDefinition for {level}");
            gameManager.instance.stateUnpause();
            _loading = false;
            yield break;
        }

        MapGenerator.Instance.Generate(def);

        // Timer & UI reset
        var gm = gameManager.instance;
        if (def.StartingTime > 0f) gm.StartingTime = def.StartingTime;

        if (level == LevelClass.Hub)
        {
            gm.gameHostageCount = 1;
        }
        else
        {
            gm.gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
        }

        gm.gameHostageSaved = 0;

        // reset private _timer
        var f = typeof(gameManager).GetField("_timer",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(gm, gm.StartingTime);

        if (def.Level != LevelClass.Hub)
            gm.updateGameGoal(0);
        else
            gm.HostageTxt.text = "–";

        gameManager.instance.stateUnpause();
        _loading = false;
        yield return null;
    }


    private void PrepareNewLevelUI(LevelDefinition def)
    {
        var gm = gameManager.instance;

        if (def.StartingTime > 0f)
            gm.StartingTime = def.StartingTime;

        // Reset counts
        gm.gameHostageSaved = 0;
        gm.gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;

        // Reset the private timer to StartingTime
        var timerField = typeof(gameManager).GetField("_timer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (timerField != null) timerField.SetValue(gm, gm.StartingTime);

        // Only update the goal (and risk a win popup) on non-hub levels:
        if (def.Level != LevelClass.Hub)
            gm.updateGameGoal(0);
        else
            gm.HostageTxt.text = "-"; // optional: show "-" in the hub
    }
}
