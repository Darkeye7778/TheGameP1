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
            // Skip nulls and the entry room (keep spawn area clean)
            if (!room || room.IsEntry) continue;

            var props = room.Properties;
            if (props == null) continue;

            // 1) Resolve base rule by archetype (Hallway / SmallRoom / DoubleRoom)
            var rule = theme.GetRule(props.Archetype);
            if (rule == null) continue;

            // 2) Optional category override (Bedroom, Armory, Cubicles, etc.)
            var catRule = GetCategoryRule(rule, room.Category);

            // 3) Decide per-room prop count (category override > base rule)
            int min = (catRule != null && catRule.MinProps >= 0) ? catRule.MinProps : rule.MinProps;
            int max = (catRule != null && catRule.MaxProps >= 0) ? catRule.MaxProps : rule.MaxProps;
            if (min < 0) min = 0;
            if (max < min) max = min;

            // 4) Collect sockets and shuffle for variety
            var sockets = room.GetComponentsInChildren<PropSocket>(true);
            if (sockets == null || sockets.Length == 0) continue;
            Shuffle(sockets, rng);

            int target = rng.Next(min, max + 1);
            int placed = 0;

            // 5) Try to place props at sockets until we hit the target
            foreach (var s in sockets)
            {
                if (placed >= target) break;

                // Resolve prefab list for this socket (category override first, else archetype fallback)
                var list = GetList(rule, catRule, s.Type);
                var prefab = PickWeighted(list, rng);
                if (!prefab) continue;

                // Placement transforms
                Vector3 pos = s.transform.position;
                Vector3 forward = (s.ForwardHint.sqrMagnitude < 1e-4f)
                    ? s.transform.forward
                    : s.transform.TransformDirection(s.ForwardHint);
                Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

                // 6) Keep doorways clear using your fixed door dimensions (2.0w × 4.0h × 0.3d)
                //    - sidePadding: little extra on width so we don’t clip frames
                //    - depthPadding: apron into the room to leave passage space
                //    - intoRoomOffset: shift the keep-out box “into” the room if the door pivot sits at the wall face
                if (!HasDoorClearance(room, s.transform.position,
                                      sidePadding: 0.05f,
                                      depthPadding: 0.35f,
                                      heightPadding: 0.0f,
                                      intoRoomOffset: 0.30f))
                {
                    continue;
                }

                // 7) Floor snap + overlap check
                if (!SnapToFloor(ref pos)) continue;
                if (!AreaFree(pos, prefab, rot, s.Clearance)) continue;

                // 8) Instantiate and register for cleanup
                var go = Object.Instantiate(prefab, pos, rot, room.transform);
                gameManager.instance.RegisterEntity(go);
                placed++;
            }
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
                                            float sidePadding = 0.05f,   // extra space left/right (m)
                                            float depthPadding = 0.35f,  // extra thickness along Z (m)
                                            float heightPadding = 0.0f,  // extra height (rarely needed)
                                            float intoRoomOffset = 0.0f) // shift the test box along +Z (m)
    {
        float halfW = (DOOR_WIDTH * 0.5f) + sidePadding;
        float halfH = (DOOR_HEIGHT * 0.5f) + heightPadding;
        float halfD = (DOOR_DEPTH * 0.5f) + depthPadding;

        var doors = room.GetComponentsInChildren<ConnectionProfile>(true);
        foreach (var c in doors)
        {
            if (!c) continue;

            // Candidate point in door-local space
            Vector3 local = c.transform.InverseTransformPoint(worldPos);

            // If the door's pivot is at the wall face (not center of thickness),
            // push the test box slightly "into" the room so we keep a clear apron.
            // (Try +halfD or a fixed offset like 0.3–0.6 depending on your layout.)
            local.z -= intoRoomOffset;

            if (Mathf.Abs(local.x) <= halfW &&
                Mathf.Abs(local.y) <= halfH &&
                Mathf.Abs(local.z) <= halfD)
            {
                // Inside the doorway volume -> NOT clear
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
            // ignore static room shell if you want; simplest is to accept empty space only:
            return false;
        }
        return true;
    }
}
