#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RoomDoorPicker : EditorWindow
{
    RoomProperties rp;
    Vector2Int size;
    List<Item> items = new List<Item>();
    int manualEntrance = -1;
    Vector2 scroll;

    class Item
    {
        public string label;
        public Vector2 pos;            // grid coords (x,y)
        public ExitDirection dir;      // 0=Z+,1=X+,2=Z-,3=X-
        public bool on;
        public Item(string l, Vector2 p, ExitDirection d, bool v = false) { label = l; pos = p; dir = d; on = v; }
    }

    [MenuItem("Tools/Rooms/Door Picker")]
    static void Open()
    {
        var w = GetWindow<RoomDoorPicker>("Door Picker");
        w.minSize = new Vector2(360, 320);
        w.RefreshFromSelection();
        w.Show();
    }

    void OnFocus() { RefreshFromSelection(); }
    void OnSelectionChange() { RefreshFromSelection(); Repaint(); }

    void RefreshFromSelection()
    {
        rp = Selection.activeObject as RoomProperties;
        items.Clear();
        manualEntrance = -1;
        if (!rp) return;

        size = rp.Size;

        // Build the canonical positions for this size
        if (size.x == 1 && size.y == 1) BuildSmall();
        else if (size.x == 1 && size.y == 2) BuildHall12();
        else if (size.x == 2 && size.y == 2) BuildDouble22();
        else ShowNotification(new GUIContent($"Unsupported size {size.x}x{size.y} (needs 1x1, 1x2, 2x2)"));

        // Precheck any that already exist (best-effort; ignores previously shifted data)
        if (rp.ConnectionPoints != null && rp.ConnectionPoints.Length > 0)
        {
            var existing = new HashSet<(int, int, int)>();
            foreach (var c in rp.ConnectionPoints)
            {
                var p = c.Transform.Position;
                existing.Add(((int)p.x, (int)p.y, (int)c.Transform.Rotation));
            }
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (existing.Contains(((int)it.pos.x, (int)it.pos.y, (int)it.dir)))
                    it.on = true;
            }
        }
    }

    // Canonical positions per your spec
    void BuildSmall()
    {
        items.Add(new Item("N", new Vector2(0, 0), ExitDirection.ZPositive));
        items.Add(new Item("S", new Vector2(0, 2), ExitDirection.ZNegative));
        items.Add(new Item("E", new Vector2(-1, 1), ExitDirection.XPositive));
        items.Add(new Item("W", new Vector2(1, 1), ExitDirection.XNegative));
    }
    void BuildHall12()
    {
        items.Add(new Item("N", new Vector2(0, 0), ExitDirection.ZPositive));
        items.Add(new Item("S", new Vector2(0, 4), ExitDirection.ZNegative));
        items.Add(new Item("E1", new Vector2(-1, 1), ExitDirection.XPositive));
        items.Add(new Item("E2", new Vector2(-1, 3), ExitDirection.XPositive));
        items.Add(new Item("W1", new Vector2(1, 1), ExitDirection.XNegative));
        items.Add(new Item("W2", new Vector2(1, 3), ExitDirection.XNegative));
    }
    void BuildDouble22()
    {
        items.Add(new Item("N-R", new Vector2(0, 0), ExitDirection.ZPositive));
        items.Add(new Item("N-L", new Vector2(-2, 0), ExitDirection.ZPositive));
        items.Add(new Item("S-R", new Vector2(0, 4), ExitDirection.ZNegative));
        items.Add(new Item("S-L", new Vector2(-2, 4), ExitDirection.ZNegative));
        items.Add(new Item("E1", new Vector2(-3, 1), ExitDirection.XPositive));
        items.Add(new Item("E2", new Vector2(-3, 3), ExitDirection.XPositive));
        items.Add(new Item("W1", new Vector2(1, 1), ExitDirection.XNegative));
        items.Add(new Item("W2", new Vector2(1, 3), ExitDirection.XNegative));
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("RoomProperties", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField(rp, typeof(RoomProperties), false);

        if (!rp)
        {
            EditorGUILayout.HelpBox("Select a RoomProperties asset.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Size: {size.x} x {size.y}");
        EditorGUILayout.Space(6);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            it.on = EditorGUILayout.ToggleLeft($"{it.label}  ({it.pos.x},{it.pos.y})  {it.dir}", it.on);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        manualEntrance = EditorGUILayout.IntField(new GUIContent("Manual Entrance Index (-1 = Auto)"), manualEntrance);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("All")) for (int i = 0; i < items.Count; i++) items[i].on = true;
            if (GUILayout.Button("None")) for (int i = 0; i < items.Count; i++) items[i].on = false;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply", GUILayout.Height(26))) Apply();
        }
    }

    void Apply()
    {
        if (!rp) return;

        // Build list of enabled doors
        var enabledIdx = new List<int>();
        for (int i = 0; i < items.Count; i++) if (items[i].on) enabledIdx.Add(i);
        if (enabledIdx.Count == 0)
        {
            rp.ConnectionPoints = new Connection[0];
            EditorUtility.SetDirty(rp);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("No doors enabled; wrote 0 connections."));
            return;
        }

        // Determine entrance: manual index if valid & enabled, otherwise auto (right-hand North then left North).
        int entrance = -1;
        if (manualEntrance >= 0 && manualEntrance < items.Count && items[manualEntrance].on)
            entrance = manualEntrance;
        else
            entrance = AutoEntranceRightThenLeft(enabledIdx);

        // If entrance is a LEFT north door (i.e., not the rightmost north), shift all saved points by +2 on X.
        bool shiftX2 = IsLeftNorth(entrance, enabledIdx);
        var conns = new List<Connection>(enabledIdx.Count - 1);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (!it.on) continue;
            if (i == entrance) continue;                      // exclude entrance from saved list

            var pos = shiftX2 ? new Vector2(it.pos.x + 2f, it.pos.y) : it.pos;
            conns.Add(new Connection
            {
                Transform = new GridTransform(pos, it.dir),
                Required = false,
                HasDoor = true,
                Odds = 0.25f
            });
        }

        rp.ConnectionPoints = conns.ToArray();
        EditorUtility.SetDirty(rp);
        AssetDatabase.SaveAssets();
        string msg = $"Saved {conns.Count} connections. Entrance {(entrance >= 0 ? items[entrance].label :"(auto none)")}, shiftX2={(shiftX2 ? "YES":"+0")}";
        ShowNotification(new GUIContent(msg));
        Debug.Log($"[DoorPicker] {rp.name}: {msg}");
    }
    int AutoEntranceRightThenLeft(List<int> enabled)
    {
        int rightIdx = -1; float rightMaxX = float.NegativeInfinity;
        int leftIdx = -1; float leftMinX = float.PositiveInfinity;

        foreach (var i in enabled)
        {
            var it = items[i];
            if (it.dir != ExitDirection.ZPositive) continue; // North only
            if (it.pos.x > rightMaxX) { rightMaxX = it.pos.x; rightIdx = i; }
            if (it.pos.x < leftMinX) { leftMinX = it.pos.x; leftIdx = i; }
        }
        if (rightIdx != -1) return rightIdx;
        if (leftIdx != -1) return leftIdx;
        return -1;
    }

    // True when chosen entrance is a North door and there exists a North door with larger X (so chosen is the left one)
    bool IsLeftNorth(int entrance, List<int> enabled)
    {
        if (entrance < 0 || entrance >= items.Count) return false;
        var chosen = items[entrance];
        if (chosen.dir != ExitDirection.ZPositive) return false;

        float maxX = float.NegativeInfinity;
        foreach (var i in enabled)
        {
            var it = items[i];
            if (it.dir != ExitDirection.ZPositive) continue;
            if (it.pos.x > maxX) maxX = it.pos.x;
        }
        return maxX > chosen.pos.x; // chosen is not the rightmost north => left-hand entrance
    }
}
#endif