using System.Collections.Generic;

// Dictionary<string,object> <-> ReplayDTO, mesmo padrão de CharacterDTOMap/ReplayEventDTOMap.
public static class ReplayDTOMap
{
    public static Dictionary<string, object> ToMap(ReplayDTO dto)
    {
        var events = new List<object>();
        if (dto.events != null)
            foreach (var e in dto.events)
                events.Add(ReplayEventDTOMap.ToMap(e));

        return new Dictionary<string, object>
        {
            { "createdAtTicks", dto.createdAtTicks },
            { "opponentCharacterId", dto.opponentCharacterId },
            { "opponentName", dto.opponentName },
            { "result", dto.result },
            { "seed", dto.seed },
            { "eventCount", dto.eventCount },
            { "roundCount", dto.roundCount },
            { "p1Snapshot", SnapshotToMap(dto.p1Snapshot) },
            { "p2Snapshot", SnapshotToMap(dto.p2Snapshot) },
            { "events", events },
        };
    }

    public static ReplayDTO FromMap(Dictionary<string, object> map)
    {
        var dto = new ReplayDTO
        {
            createdAtTicks = GetLong(map, "createdAtTicks"),
            opponentCharacterId = GetString(map, "opponentCharacterId"),
            opponentName = GetString(map, "opponentName"),
            result = GetString(map, "result"),
            seed = GetInt(map, "seed"),
            eventCount = GetInt(map, "eventCount"),
            roundCount = GetInt(map, "roundCount"),
        };

        if (map.TryGetValue("p1Snapshot", out var p1Obj) && p1Obj is Dictionary<string, object> p1Map)
            dto.p1Snapshot = SnapshotFromMap(p1Map);
        if (map.TryGetValue("p2Snapshot", out var p2Obj) && p2Obj is Dictionary<string, object> p2Map)
            dto.p2Snapshot = SnapshotFromMap(p2Map);

        if (map.TryGetValue("events", out var evObj) && evObj is List<object> evList)
            foreach (var item in evList)
                if (item is Dictionary<string, object> evMap)
                    dto.events.Add(ReplayEventDTOMap.FromMap(evMap));

        return dto;
    }

    private static Dictionary<string, object> SnapshotToMap(ReplayPlayerSnapshotDTO s)
    {
        if (s == null) return null;

        var weapons = new List<object>();
        foreach (var w in s.weapons) weapons.Add(new Dictionary<string, object> { { "name", w.name }, { "tier", w.tier } });
        var skills = new List<object>();
        foreach (var sk in s.skills) skills.Add(new Dictionary<string, object> { { "name", sk.name }, { "tier", sk.tier } });
        var pets = new List<object>();
        foreach (var p in s.pets) pets.Add(new Dictionary<string, object> { { "type", p.type }, { "tier", p.tier } });

        return new Dictionary<string, object>
        {
            { "profileName", s.profileName },
            { "maxHealth", s.maxHealth },
            { "str", s.str },
            { "agility", s.agility },
            { "speed", s.speed },
            { "armor", s.armor },
            { "evasion", s.evasion },
            { "accuracy", s.accuracy },
            { "initiative", s.initiative },
            { "reversal", s.reversal },
            { "counter", s.counter },
            { "blockBonus", s.blockBonus },
            { "reversalAfterBlock", s.reversalAfterBlock },
            { "criticalChance", s.criticalChance },
            { "hitSpeed", s.hitSpeed },
            { "weapons", weapons },
            { "skills", skills },
            { "pets", pets },
        };
    }

    private static ReplayPlayerSnapshotDTO SnapshotFromMap(Dictionary<string, object> map)
    {
        var s = new ReplayPlayerSnapshotDTO
        {
            profileName = GetString(map, "profileName"),
            maxHealth = GetInt(map, "maxHealth"),
            str = GetInt(map, "str"),
            agility = GetInt(map, "agility"),
            speed = GetInt(map, "speed"),
            armor = GetFloat(map, "armor"),
            evasion = GetFloat(map, "evasion"),
            accuracy = GetFloat(map, "accuracy"),
            initiative = GetInt(map, "initiative"),
            reversal = GetFloat(map, "reversal"),
            counter = GetFloat(map, "counter"),
            blockBonus = GetFloat(map, "blockBonus"),
            reversalAfterBlock = GetFloat(map, "reversalAfterBlock"),
            criticalChance = GetFloat(map, "criticalChance"),
            hitSpeed = GetFloat(map, "hitSpeed"),
        };

        if (map.TryGetValue("weapons", out var wObj) && wObj is List<object> wList)
            foreach (var w in wList)
                if (w is Dictionary<string, object> wMap)
                    s.weapons.Add(new WeaponTierRef { name = GetString(wMap, "name"), tier = GetInt(wMap, "tier") });

        if (map.TryGetValue("skills", out var sObj) && sObj is List<object> sList)
            foreach (var sk in sList)
                if (sk is Dictionary<string, object> skMap)
                    s.skills.Add(new SkillTierRef { name = GetString(skMap, "name"), tier = GetInt(skMap, "tier") });

        if (map.TryGetValue("pets", out var pObj) && pObj is List<object> pList)
            foreach (var p in pList)
                if (p is Dictionary<string, object> pMap)
                    s.pets.Add(new PetTierRef { type = GetString(pMap, "type"), tier = GetInt(pMap, "tier") });

        return s;
    }

    private static string GetString(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) ? v as string : null;

    private static int GetInt(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToInt32(v) : 0;

    private static long GetLong(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToInt64(v) : 0L;

    private static float GetFloat(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToSingle(v) : 0f;
}
