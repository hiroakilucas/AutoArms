using System.Collections.Generic;

// Dictionary<string,object> <-> ReplayEventDTO, mesmo padrão de CharacterDTOMap (ver
// CharacterDTOMap.cs pro motivo de usar Dictionary manual em vez de [FirestoreData]).
//
// ToMap só grava no documento os campos que fogem do valor-padrão daquele evento específico —
// diferente de JsonUtility (que sempre serializa todo campo, mesmo default), o Firestore não tem
// esse problema, então um TurnStart vira só {type, playerIndex} e os outros ~28 campos de
// ReplayEventDTO nunca aparecem no documento pra esse evento. `type`/`playerIndex` são sempre
// gravados (identificam o evento mesmo quando playerIndex==0/P1, que coincide com o default do
// campo — sem eles o evento ficaria ambíguo/vazio no documento).
public static class ReplayEventDTOMap
{
    public static Dictionary<string, object> ToMap(ReplayEventDTO e)
    {
        var map = new Dictionary<string, object>
        {
            { "type", e.type },
            { "playerIndex", e.playerIndex },
        };

        if (e.targetIndex != 0) map["targetIndex"] = e.targetIndex;
        if (e.damage != 0) map["damage"] = e.damage;
        if (e.isCrit) map["isCrit"] = true;
        if (e.isCombo) map["isCombo"] = true;
        if (e.isThrow) map["isThrow"] = true;
        if (e.isRetaliation) map["isRetaliation"] = true;
        if (e.isDodged) map["isDodged"] = true;
        if (e.isBlocked) map["isBlocked"] = true;
        if (e.isFierceBrute) map["isFierceBrute"] = true;
        if (e.newHp != 0) map["newHp"] = e.newHp;
        if (e.maxHp != 0) map["maxHp"] = e.maxHp;
        if (e.extraActions != 0) map["extraActions"] = e.extraActions;
        if (e.healAmount != 0) map["healAmount"] = e.healAmount;
        if (e.pulseCount != 0) map["pulseCount"] = e.pulseCount;
        if (e.newDefenderHp != 0) map["newDefenderHp"] = e.newDefenderHp;
        if (e.newAttackerHp != 0) map["newAttackerHp"] = e.newAttackerHp;
        if (!string.IsNullOrEmpty(e.weaponName)) map["weaponName"] = e.weaponName;

        if (e.ffWeapons != null && e.ffWeapons.Count > 0) map["ffWeapons"] = new List<object>(e.ffWeapons);
        if (e.ffDamages != null && e.ffDamages.Count > 0) map["ffDamages"] = IntListToObjects(e.ffDamages);
        if (e.ffHpAfter != null && e.ffHpAfter.Count > 0) map["ffHpAfter"] = IntListToObjects(e.ffHpAfter);

        if (e.bombTargets != null && e.bombTargets.Count > 0) map["bombTargets"] = IntListToObjects(e.bombTargets);
        if (e.bombTargetDamages != null && e.bombTargetDamages.Count > 0) map["bombTargetDamages"] = IntListToObjects(e.bombTargetDamages);
        if (e.bombTargetHp != null && e.bombTargetHp.Count > 0) map["bombTargetHp"] = IntListToObjects(e.bombTargetHp);
        if (e.netFreedTargets != null && e.netFreedTargets.Count > 0) map["netFreedTargets"] = IntListToObjects(e.netFreedTargets);

        if (e.bombPetIndexes != null && e.bombPetIndexes.Count > 0) map["bombPetIndexes"] = IntListToObjects(e.bombPetIndexes);
        if (e.bombPetHp != null && e.bombPetHp.Count > 0) map["bombPetHp"] = IntListToObjects(e.bombPetHp);

        if (e.petIndex != -1) map["petIndex"] = e.petIndex;
        if (e.targetPetIndex != -1) map["targetPetIndex"] = e.targetPetIndex;
        if (e.targetIsPet) map["targetIsPet"] = true;
        if (e.newTargetHp != 0) map["newTargetHp"] = e.newTargetHp;
        if (e.newTargetMaxHp != 0) map["newTargetMaxHp"] = e.newTargetMaxHp;
        if (e.shieldIntercept) map["shieldIntercept"] = true;
        if (e.petShieldAbsorb) map["petShieldAbsorb"] = true;

        return map;
    }

    public static ReplayEventDTO FromMap(Dictionary<string, object> map) => new ReplayEventDTO
    {
        type = GetString(map, "type"),
        playerIndex = GetInt(map, "playerIndex"),
        targetIndex = GetInt(map, "targetIndex"),
        damage = GetInt(map, "damage"),
        isCrit = GetBool(map, "isCrit"),
        isCombo = GetBool(map, "isCombo"),
        isThrow = GetBool(map, "isThrow"),
        isRetaliation = GetBool(map, "isRetaliation"),
        isDodged = GetBool(map, "isDodged"),
        isBlocked = GetBool(map, "isBlocked"),
        isFierceBrute = GetBool(map, "isFierceBrute"),
        newHp = GetInt(map, "newHp"),
        maxHp = GetInt(map, "maxHp"),
        extraActions = GetInt(map, "extraActions"),
        healAmount = GetInt(map, "healAmount"),
        pulseCount = GetInt(map, "pulseCount"),
        newDefenderHp = GetInt(map, "newDefenderHp"),
        newAttackerHp = GetInt(map, "newAttackerHp"),
        weaponName = GetString(map, "weaponName"),
        ffWeapons = GetStringList(map, "ffWeapons"),
        ffDamages = GetIntList(map, "ffDamages"),
        ffHpAfter = GetIntList(map, "ffHpAfter"),
        bombTargets = GetIntList(map, "bombTargets"),
        bombTargetDamages = GetIntList(map, "bombTargetDamages"),
        bombTargetHp = GetIntList(map, "bombTargetHp"),
        netFreedTargets = GetIntList(map, "netFreedTargets"),
        bombPetIndexes = GetIntList(map, "bombPetIndexes"),
        bombPetHp = GetIntList(map, "bombPetHp"),
        petIndex = map.ContainsKey("petIndex") ? GetInt(map, "petIndex") : -1,
        targetPetIndex = map.ContainsKey("targetPetIndex") ? GetInt(map, "targetPetIndex") : -1,
        targetIsPet = GetBool(map, "targetIsPet"),
        newTargetHp = GetInt(map, "newTargetHp"),
        newTargetMaxHp = GetInt(map, "newTargetMaxHp"),
        shieldIntercept = GetBool(map, "shieldIntercept"),
        petShieldAbsorb = GetBool(map, "petShieldAbsorb"),
    };

    // Mesmos helpers defensivos de CharacterDTOMap — valores vindos do Firestore chegam como
    // `object` e o tipo numérico concreto pode variar (long/double/int).
    private static string GetString(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) ? v as string : null;

    private static bool GetBool(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v is bool b && b;

    private static int GetInt(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToInt32(v) : 0;

    private static List<int> GetIntList(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || !(v is List<object> list)) return null;
        var result = new List<int>(list.Count);
        foreach (var item in list) result.Add(System.Convert.ToInt32(item));
        return result;
    }

    private static List<string> GetStringList(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || !(v is List<object> list)) return null;
        var result = new List<string>(list.Count);
        foreach (var item in list) result.Add(item as string);
        return result;
    }

    private static List<object> IntListToObjects(List<int> src)
    {
        var list = new List<object>(src.Count);
        foreach (var v in src) list.Add(v);
        return list;
    }
}
