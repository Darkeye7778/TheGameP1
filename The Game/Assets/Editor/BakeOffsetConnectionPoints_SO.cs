#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class BakeConnectionPoints_SO : EditorWindow
{
    enum LayoutPreset
    {
        Square20_3,  // 20.3 x 20.3 room we did earlier
        SmallRoom,   // your new small room 3-door layout
        Hallway      // your hallway layout
    }

    LayoutPreset preset = LayoutPreset.Square20_3;

    // Master toggles for each entry in the active preset
    // We will rebuild these labels and toggles when preset changes
    Vector2[] activeCoords = Array.Empty<Vector2>();
    ExitDirection[] activeFaces = Array.Empty<ExitDirection>();
    string[] activeLabels = Array.Empty<string>();
    bool[] activeChecks = Array.Empty<bool>();

    // Options
    bool forceUseSerializedArray = true; // sets UsePrefabConnectionMarkers = false
    bool setHasDoor = true;
    bool setRequired = false;
    float defaultOdds = 1f;

    // Cache last preset to refresh UI
    LayoutPreset lastPreset;

    [MenuItem("Tools/Rooms/Bake Connection Points (Presets)")]
    public static void Open() => GetWindow<BakeConnectionPoints_SO>("Bake Connections");

    void OnEnable()
    {
        lastPreset = (LayoutPreset)(-1);
        RefreshPreset();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Bake Connection Points -> RoomProperties (ScriptableObject)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        var newPreset = (LayoutPreset)EditorGUILayout.EnumPopup("Preset", preset);
        if (newPreset != preset)
        {
            preset = newPreset;
            RefreshPreset();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Select doors to bake:");

        if (activeLabels.Length == 0)
        {
            EditorGUILayout.HelpBox("No entries in this preset.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < activeLabels.Length; i++)
            {
                activeChecks[i] = EditorGUILayout.ToggleLeft(
                    activeLabels[i] + "  (" + activeCoords[i].x.ToString("0.##") + ", " + activeCoords[i].y.ToString("0.##") + ")  " + activeFaces[i].ToString(),
                    activeChecks[i]
                );
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Write Options");
        forceUseSerializedArray = EditorGUILayout.Toggle("Force Use Serialized Array", forceUseSerializedArray);
        setHasDoor = EditorGUILayout.Toggle("Set HasDoor = true", setHasDoor);
        setRequired = EditorGUILayout.Toggle("Set Required = true", setRequired);
        defaultOdds = EditorGUILayout.Slider("Default Odds", defaultOdds, 0f, 1f);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Bake to SELECTED", GUILayout.Height(28)))
            BakeToSelection();
    }

    void RefreshPreset()
    {
        // Rebuild arrays according to the chosen preset.
        // All coordinates are grid units, origin at entry (0,0).

        List<Vector2> coords = new List<Vector2>();
        List<ExitDirection> faces = new List<ExitDirection>();
        List<string> labels = new List<string>();
        List<bool> checks = new List<bool>();

        if (preset == LayoutPreset.Square20_3)
        {
            // 20.3 x 20.3 room with 0.03 offsets
            // South wall (z-)
            Add(coords, faces, labels, checks, "South Entry", new Vector2(0.00f, 0.00f), ExitDirection.South, false); // default off
            Add(coords, faces, labels, checks, "South Left", new Vector2(-2.00f, 0.00f), ExitDirection.South, true);

            // West wall (x-)
            Add(coords, faces, labels, checks, "West W1", new Vector2(-3.03f, 1.03f), ExitDirection.West, true);
            Add(coords, faces, labels, checks, "West W2", new Vector2(-3.03f, 3.03f), ExitDirection.West, true);

            // North wall (z+)
            Add(coords, faces, labels, checks, "North N2", new Vector2(-2.00f, 4.06f), ExitDirection.North, true);
            Add(coords, faces, labels, checks, "North N1", new Vector2(0.00f, 4.06f), ExitDirection.North, true);

            // East wall (x+)
            Add(coords, faces, labels, checks, "East E2", new Vector2(1.03f, 1.03f), ExitDirection.East, true);
            Add(coords, faces, labels, checks, "East E1", new Vector2(1.03f, 3.03f), ExitDirection.East, true);
        }
        else if (preset == LayoutPreset.SmallRoom)
        {
            // Your spec:
            // Doors relative to entry (0,0):
            // Left:  (-1.03, 1.03) on x- wall -> West
            // Opposite: (0, 2.06) on z+ wall -> North
            // Right: (1.03, 1.03) on x+ wall -> East
            // Entry (0,0) at z- wall -> South (usually off)
            Add(coords, faces, labels, checks, "South Entry", new Vector2(0.00f, 0.00f), ExitDirection.South, false);
            Add(coords, faces, labels, checks, "West", new Vector2(-1.03f, 1.03f), ExitDirection.West, true);
            Add(coords, faces, labels, checks, "North", new Vector2(0.00f, 2.06f), ExitDirection.North, true);
            Add(coords, faces, labels, checks, "East", new Vector2(1.03f, 1.03f), ExitDirection.East, true);
        }
        else if (preset == LayoutPreset.Hallway)
        {
            // Your spec:
            // Left wall (x-):  (-1.03, 1.03), (-1.03, 3.03) -> West
            // Opposite wall (z+): (0, 4.06) -> North
            // Right wall (x+): (1.03, 1.03), (1.03, 3.03) -> East
            // Entry (0,0) at z- wall -> South (usually off)
            Add(coords, faces, labels, checks, "South Entry", new Vector2(0.00f, 0.00f), ExitDirection.South, false);
            Add(coords, faces, labels, checks, "West A", new Vector2(-1.03f, 1.03f), ExitDirection.West, true);
            Add(coords, faces, labels, checks, "West B", new Vector2(-1.03f, 3.03f), ExitDirection.West, true);
            Add(coords, faces, labels, checks, "North", new Vector2(0.00f, 4.06f), ExitDirection.North, true);
            Add(coords, faces, labels, checks, "East A", new Vector2(1.03f, 1.03f), ExitDirection.East, true);
            Add(coords, faces, labels, checks, "East B", new Vector2(1.03f, 3.03f), ExitDirection.East, true);
        }

        activeCoords = coords.ToArray();
        activeFaces = faces.ToArray();
        activeLabels = labels.ToArray();
        activeChecks = checks.ToArray();
        lastPreset = preset;
    }

    void Add(List<Vector2> coords, List<ExitDirection> faces, List<string> labels, List<bool> checks,
             string label, Vector2 pos, ExitDirection face, bool onByDefault)
    {
        labels.Add(label);
        coords.Add(pos);
        faces.Add(face);
        checks.Add(onByDefault);
    }

    void BakeToSelection()
    {
        var targets = CollectRoomPropertiesFromSelection();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[BakeConnections] No RoomProperties found in selection (select RoomProperties assets, or prefabs/scene objects with RoomProfile).");
            return;
        }

        int changed = 0;
        Undo.IncrementCurrentGroup();

        foreach (var rp in targets)
        {
            if (!rp) continue;
            Undo.RecordObject(rp, "Bake ConnectionPoints");

            var list = new List<Connection>();
            for (int i = 0; i < activeCoords.Length; i++)
            {
                if (!activeChecks[i]) continue;
                list.Add(MakeConn(activeCoords[i].x, activeCoords[i].y, activeFaces[i]));
            }

            rp.ConnectionPoints = list.ToArray();
            if (forceUseSerializedArray) rp.UsePrefabConnectionMarkers = false;

            EditorUtility.SetDirty(rp);
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BakeConnections] Wrote " + changed + " RoomProperties asset(s).");
    }

    List<RoomProperties> CollectRoomPropertiesFromSelection()
    {
        var result = new List<RoomProperties>();
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) return result;

        foreach (var o in objs)
        {
            // Direct RoomProperties asset
            if (o is RoomProperties rpAsset)
            {
                result.Add(rpAsset);
                continue;
            }

            // Prefab asset: open, find RoomProfile -> Properties
            var path = AssetDatabase.GetAssetPath(o);
            bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            if (isPrefabAsset)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root)
                {
                    var prof = root.GetComponentInChildren<RoomProfile>(true);
                    if (prof && prof.Properties) result.Add(prof.Properties);
                }
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            // Scene object
            if (o is GameObject go)
            {
                var prof = go.GetComponentInChildren<RoomProfile>(true);
                if (prof && prof.Properties) result.Add(prof.Properties);
            }
        }

        return result.Distinct().ToList();
    }

    Connection MakeConn(float gx, float gz, ExitDirection face)
    {
        return new Connection
        {
            Transform = new GridTransform(new Vector2(gx, gz), face),
            Required = setRequired,
            HasDoor = setHasDoor,
            IsEntrance = false,
            Odds = Mathf.Clamp01(defaultOdds)
        };
    }
}
#endif
