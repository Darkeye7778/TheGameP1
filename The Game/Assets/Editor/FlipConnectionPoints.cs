#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class FlipConnectionRotations : EditorWindow
{
    // Options
    bool flipRotations = true;          // main action
    bool forceDisablePrefabMarkers = true;

    // Optional position tweaks (applied AFTER flip, in grid units)
    bool applyNudge = false;
    float nudgeX = 0f;
    float nudgeZ = 0f;

    // Optional global mirrors (use with care)
    bool negateAllX = false;
    bool negateAllZ = false;

    // Entry CP helper for start rooms
    bool ensureEntryExists = false;
    bool setEntryHasDoor = true;
    bool setEntryRequired = false;

    // Entry CP definition
    Vector2 entryPos = new Vector2(0f, 0f);    // (gridX, gridZ)
    ExitDirection entryDir = ExitDirection.South;

    // Optional rounding to tame floating point noise
    bool roundPositions = true;
    int roundDigits = 3;

    [MenuItem("Tools/Rooms/Flip Connection Rotations (Batch)")]
    static void Open() => GetWindow<FlipConnectionRotations>("Flip Rotations");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Batch edit RoomProperties.ConnectionPoints", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        flipRotations = EditorGUILayout.Toggle("Flip Rotations to Opposites", flipRotations);
        forceDisablePrefabMarkers = EditorGUILayout.Toggle("Force UsePrefabConnectionMarkers = false", forceDisablePrefabMarkers);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Optional Position Adjustments (grid units)", EditorStyles.boldLabel);
        applyNudge = EditorGUILayout.Toggle("Apply constant nudge", applyNudge);
        if (applyNudge)
        {
            nudgeX = EditorGUILayout.FloatField("Nudge X", nudgeX);
            nudgeZ = EditorGUILayout.FloatField("Nudge Z", nudgeZ);
        }

        negateAllX = EditorGUILayout.Toggle("Negate ALL X (mirror left/right)", negateAllX);
        negateAllZ = EditorGUILayout.Toggle("Negate ALL Z (mirror front/back)", negateAllZ);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Ensure Entry CP", EditorStyles.boldLabel);
        ensureEntryExists = EditorGUILayout.Toggle("Ensure (0,0,S) exists", ensureEntryExists);
        if (ensureEntryExists)
        {
            entryPos.x = EditorGUILayout.FloatField("Entry X", entryPos.x);
            entryPos.y = EditorGUILayout.FloatField("Entry Z", entryPos.y);
            entryDir = (ExitDirection)EditorGUILayout.EnumPopup("Entry Direction", entryDir);
            setEntryHasDoor = EditorGUILayout.Toggle("Entry HasDoor = true", setEntryHasDoor);
            setEntryRequired = EditorGUILayout.Toggle("Entry Required = true", setEntryRequired);
        }

        EditorGUILayout.Space(6);
        roundPositions = EditorGUILayout.Toggle("Round positions", roundPositions);
        if (roundPositions)
        {
            roundDigits = Mathf.Clamp(EditorGUILayout.IntField("Round digits", roundDigits), 0, 6);
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Process SELECTED RoomProperties / Prefabs / Scene Objects", GUILayout.Height(28)))
        {
            ProcessSelection();
        }
    }

    void ProcessSelection()
    {
        var targets = CollectRoomPropertiesFromSelection();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[FlipConnectionRotations] No RoomProperties found. Select RoomProperties assets or prefab/scene objects with RoomProfile.");
            return;
        }

        int changedAssets = 0, totalCP = 0, flipped = 0, nudged = 0, mirrored = 0, addedEntry = 0;

        Undo.IncrementCurrentGroup();

        foreach (var rp in targets.Distinct())
        {
            if (!rp) continue;
            var conns = rp.ConnectionPoints;
            if (conns == null) conns = new Connection[0];

            bool assetChanged = false;

            // Flip rotations
            if (flipRotations && conns.Length > 0)
            {
                for (int i = 0; i < conns.Length; i++)
                {
                    var c = conns[i];
                    c.Transform.Rotation = Opposite(c.Transform.Rotation);
                    conns[i] = c;
                    flipped++;
                }
                assetChanged = true;
            }

            // Mirrors and nudges
            if ((negateAllX || negateAllZ || applyNudge) && conns.Length > 0)
            {
                for (int i = 0; i < conns.Length; i++)
                {
                    var c = conns[i];
                    var p = c.Transform.Position;

                    if (negateAllX) { p.x = -p.x; mirrored++; }
                    if (negateAllZ) { p.y = -p.y; mirrored++; }
                    if (applyNudge) { p.x += nudgeX; p.y += nudgeZ; nudged++; }

                    if (roundPositions)
                    {
                        p.x = (float)System.Math.Round(p.x, roundDigits);
                        p.y = (float)System.Math.Round(p.y, roundDigits);
                    }

                    c.Transform.Position = p;
                    conns[i] = c;
                }
                assetChanged = true;
            }

            // Ensure entry CP
            if (ensureEntryExists)
            {
                bool hasEntry = false;
                for (int i = 0; i < conns.Length; i++)
                {
                    if (Approximately(conns[i].Transform.Position, entryPos) &&
                        conns[i].Transform.Rotation == entryDir)
                    {
                        hasEntry = true;
                        break;
                    }
                }

                if (!hasEntry)
                {
                    var entry = new Connection
                    {
                        Transform = new GridTransform(entryPos, entryDir),
                        HasDoor = setEntryHasDoor,
                        Required = setEntryRequired,
                        IsEntrance = true,
                        Odds = 1f
                    };
                    var list = conns.ToList();
                    list.Add(entry);
                    conns = list.ToArray();
                    assetChanged = true;
                    addedEntry++;
                }
            }

            // Apply back
            if (assetChanged)
            {
                Undo.RecordObject(rp, "Flip Connection Rotations");
                rp.ConnectionPoints = conns;
                if (forceDisablePrefabMarkers) rp.UsePrefabConnectionMarkers = false;
                EditorUtility.SetDirty(rp);
                changedAssets++;
                totalCP += conns.Length;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FlipConnectionRotations] Changed assets: " + changedAssets +
                  ", total CP now: " + totalCP +
                  ", flipped: " + flipped +
                  ", mirrored ops: " + mirrored +
                  ", nudged: " + nudged +
                  ", added entry: " + addedEntry);
    }

    List<RoomProperties> CollectRoomPropertiesFromSelection()
    {
        var result = new List<RoomProperties>();
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) return result;

        foreach (var o in objs)
        {
            // Direct asset
            if (o is RoomProperties rpAsset)
            {
                result.Add(rpAsset);
                continue;
            }

            // Prefab asset
            var path = AssetDatabase.GetAssetPath(o);
            bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
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

        return result;
    }

    ExitDirection Opposite(ExitDirection dir)
    {
        switch (dir)
        {
            // If your enum uses XPositive/ZPositive names, map accordingly:
            case ExitDirection.ZPositive: return ExitDirection.ZNegative;
            case ExitDirection.ZNegative: return ExitDirection.ZPositive;
            case ExitDirection.XPositive: return ExitDirection.XNegative;
            case ExitDirection.XNegative: return ExitDirection.XPositive;
        }
        return dir;
    }

    bool Approximately(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= 1e-6f;
    }
}
#endif
