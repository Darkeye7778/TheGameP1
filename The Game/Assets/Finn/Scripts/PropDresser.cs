using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public static class PropDresser
{
    static readonly float DOOR_WIDTH = 2.0f;
    static readonly float DOOR_HEIGHT = 4.0f;
    static readonly float DOOR_DEPTH = 0.3f;

    public static void DressRooms(List<RoomProfile> rooms, PropTheme theme, System.Random rng)
    {
        if (theme == null || rooms == null) return;

        foreach (var room in rooms)
        {
            /*if (!room || room.IsEntry) continue;

            var props = room.Properties;
            if (props == null) continue;

            var rule = theme.GetRule(props.Archetype);
            if (rule == null) continue;

            var catRule = GetCategoryRule(rule, room.Category);

            int min = (catRule != null && catRule.MinProps >= 0) ? catRule.MinProps : rule.MinProps;
            int max = (catRule != null && catRule.MaxProps >= 0) ? catRule.MaxProps : rule.MaxProps;
            if (min < 0) min = 0;
            if (max < min) max = min;

            var sockets = room.GetComponentsInChildren<PropSocket>(true);
            if (sockets == null || sockets.Length == 0) continue;
            Shuffle(sockets, rng);

            int target = rng.Next(min, max + 1);
            int placed = 0;

            foreach (var s in sockets)
            {
                if (placed >= target) break;

                var list = GetList(rule, catRule, s.Type);
                var prefab = PickWeighted(list, rng);
                if (!prefab) continue;

                Vector3 pos = s.transform.position;
                Vector3 forward = (s.ForwardHint.sqrMagnitude < 1e-4f)
                    ? s.transform.forward
                    : s.transform.TransformDirection(s.ForwardHint);
                Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

                var model = room.transform.Find("model");
                var roomRoot = model ? model : room.transform;

                Vector3 local = roomRoot.InverseTransformPoint(s.transform.position);

                float halfW = DOOR_WIDTH * 0.5f + 0.10f;
                float halfD = 0.50f;

                if (IsInsideAnyDoorBand(local, room.Properties, MapGenerator.GRID_SIZE, halfW, halfD))
                    continue;

                if (!SnapToFloor(ref pos)) continue;
                if (!AreaFree(pos, prefab, rot, s.Clearance)) continue;

                var go = Object.Instantiate(prefab, pos, rot, room.transform);
                gameManager.instance.RegisterEntity(go);
                placed++;
            }*/
        }
    }

    private static PropTheme.CategoryRule GetCategoryRule(PropTheme.Rule rule, RoomCategory category)
    {
        if (rule == null || rule.Categories == null) return null;
        for (int i = 0; i < rule.Categories.Length; i++)
        {
            var cr = rule.Categories[i];
            if (cr != null && cr.Category == category) return cr;
        }
        return null;
    }

    private static WeightedPrefab[] GetList(PropTheme.Rule baseRule, PropTheme.CategoryRule catRule, SocketType socket)
    {
        if (catRule != null)
        {
            var catList = socket switch
            {
                SocketType.CenterSmall => catRule.CenterSmall,
                SocketType.CenterLarge => catRule.CenterLarge,
                SocketType.Corner => catRule.Corner,
                SocketType.Wall => catRule.Wall,
                SocketType.Ceiling => catRule.Ceiling,
                _ => null
            };
            if (catList != null && catList.Length > 0) return catList;
        }

        return socket switch
        {
            SocketType.CenterSmall => baseRule.CenterSmall,
            SocketType.CenterLarge => baseRule.CenterLarge,
            SocketType.Corner => baseRule.Corner,
            SocketType.Wall => baseRule.Wall,
            SocketType.Ceiling => baseRule.Ceiling,
            _ => null
        };
    }

    static GameObject PickWeighted(WeightedPrefab[] arr, System.Random rng)
    {
        if (arr == null || arr.Length == 0) return null;
        float sum = 0; foreach (var w in arr) sum += Mathf.Max(0, w.Weight);
        if (sum <= 0) return arr[0].Prefab;
        float r = (float)(rng.NextDouble() * sum), c = 0;
        foreach (var w in arr) { c += Mathf.Max(0, w.Weight); if (r <= c) return w.Prefab; }
        return arr[^1].Prefab;
    }

    static void Shuffle<T>(IList<T> a, System.Random rng) { for (int i = a.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); } }

    private static bool HasDoorClearance(RoomProfile room, Vector3 worldPos,
                                            float sidePadding = 0.05f,   
                                            float depthPadding = 0.35f,  
                                            float heightPadding = 0.0f,  
                                            float intoRoomOffset = 0.0f) 
    {
        float halfW = (DOOR_WIDTH * 0.5f) + sidePadding;
        float halfH = (DOOR_HEIGHT * 0.5f) + heightPadding;
        float halfD = (DOOR_DEPTH * 0.5f) + depthPadding;

        var doors = room.GetComponentsInChildren<ConnectionProfile>(true);
        foreach (var c in doors)
        {
            if (!c) continue;

            Vector3 local = c.transform.InverseTransformPoint(worldPos);

            local.z -= intoRoomOffset;

            if (Mathf.Abs(local.x) <= halfW &&
                Mathf.Abs(local.y) <= halfH &&
                Mathf.Abs(local.z) <= halfD)
            {
                return false;
            }
        }
        return true;
    }

    static bool SnapToFloor(ref Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos + Vector3.up, out var hit, 2f, NavMesh.AllAreas)) { pos = hit.position; return true; }
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var rh, 4f)) { pos = rh.point; return true; }
        return false;
    }

    static bool AreaFree(Vector3 pos, GameObject prefab, Quaternion rot, float clearance)
    {
        var rends = prefab.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return true;
        var b = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        var half = (b.size + Vector3.one * (clearance * 2f)) * 0.5f;
        var hits = Physics.OverlapBox(pos + rot * (b.center - prefab.transform.position), half, rot, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            return false;
        }
        return true;
    }

    static bool IsInsideAnyDoorBand(Vector3 localPos, RoomProperties props, float grid, float doorHalfWidth, float doorHalfDepth)
    {
        if (props == null || props.ConnectionPoints == null) return false;

        foreach (var c in props.ConnectionPoints)
        {
            var p2 = c.Transform.Position;              // grid coords
            int r = ((int)c.Transform.Rotation) & 3;    // 0=Z+, 1=X+, 2=Z-, 3=X-
            Vector3 center = new Vector3(grid * p2.x, 0f, grid * p2.y);

            Vector2 tan = r == 0 ? new Vector2(0, 1) :
                          r == 1 ? new Vector2(1, 0) :
                          r == 2 ? new Vector2(0, -1) : new Vector2(-1, 0);
            Vector2 nor = new Vector2(-tan.y, tan.x);

            Vector2 d = new Vector2(localPos.x - center.x, localPos.z - center.z);
            float dt = Vector2.Dot(d, tan);
            float dn = Vector2.Dot(d, nor);

            if (Mathf.Abs(dt) <= doorHalfWidth && Mathf.Abs(dn) <= doorHalfDepth)
                return true;
        }
        return false;
    }
}
