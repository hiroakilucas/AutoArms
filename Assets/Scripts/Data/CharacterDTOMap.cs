using System.Collections.Generic;

// Converte CharacterDTO <-> Dictionary<string, object> pra gravar/ler no Firestore (Fatia 3,
// 2026-07-15). Escolha deliberada: NAO usar os atributos [FirestoreData]/[FirestoreProperty] do
// SDK (mapeamento automatico) - essa camada exige propriedades (get/set), não os campos publicos
// que CharacterDTO ja usa pra funcionar com JsonUtility (LocalSaveService), que por sua vez só
// serializa campos, não propriedades. Ter os dois (Firestore por propriedade, JsonUtility por
// campo) ia forçar duas classes DTO separadas ou uma mistura arriscada. Mapeamento manual via
// Dictionary é mais verboso mas usa só a API mais estável/documentada do Firestore
// (SetAsync(IDictionary)/DocumentSnapshot.ToDictionary()), sem depender de um comportamento de
// serialização automática que não pôde ser conferido antes de escrever este código.
public static class CharacterDTOMap
{
    public static Dictionary<string, object> ToMap(CharacterDTO dto)
    {
        var weapons = new List<object>();
        foreach (var w in dto.weapons)
            weapons.Add(new Dictionary<string, object> { { "name", w.name }, { "tier", w.tier } });

        var skills = new List<object>();
        foreach (var s in dto.skills)
            skills.Add(new Dictionary<string, object> { { "name", s.name }, { "tier", s.tier } });

        var pets = new List<object>();
        foreach (var p in dto.pets)
            pets.Add(new Dictionary<string, object> { { "type", p.type }, { "tier", p.tier } });

        return new Dictionary<string, object>
        {
            { "characterId", dto.characterId },
            { "profileName", dto.profileName },
            { "level", dto.level },
            { "winRate", dto.winRate },
            { "xpCurrent", dto.xpCurrent },
            { "xpRequired", dto.xpRequired },
            { "battlesRemaining", dto.battlesRemaining },
            { "isFavorite", dto.isFavorite },
            { "rarity", dto.rarity },
            { "maxHealth", dto.maxHealth },
            { "str", dto.str },
            { "agility", dto.agility },
            { "speed", dto.speed },
            { "armor", dto.armor },
            { "evasion", dto.evasion },
            { "accuracy", dto.accuracy },
            { "initiative", dto.initiative },
            { "reversal", dto.reversal },
            { "counter", dto.counter },
            { "blockBonus", dto.blockBonus },
            { "reversalAfterBlock", dto.reversalAfterBlock },
            { "criticalChance", dto.criticalChance },
            { "hitSpeed", dto.hitSpeed },
            { "weapons", weapons },
            { "skills", skills },
            { "pets", pets },
            // Timestamp do CLIENTE (não FieldValue.ServerTimestamp) — de propósito, pra comparar
            // direto com LocalSaveService.GetCachedUpdatedAtTicks (mesma unidade/relógio) na
            // reconciliação "último gravado ganha". Simplificação já assumida no plano (sem
            // merge de verdade, confia no relógio do cliente) — ok pro cenário de 1 dispositivo
            // por vez de hoje.
            { "updatedAtTicks", dto.updatedAtTicks },
        };
    }

    public static CharacterDTO FromMap(Dictionary<string, object> map)
    {
        var dto = new CharacterDTO
        {
            characterId = GetString(map, "characterId"),
            profileName = GetString(map, "profileName"),
            level = GetInt(map, "level"),
            winRate = GetFloat(map, "winRate"),
            xpCurrent = GetInt(map, "xpCurrent"),
            xpRequired = GetInt(map, "xpRequired"),
            battlesRemaining = GetInt(map, "battlesRemaining"),
            isFavorite = GetBool(map, "isFavorite"),
            rarity = GetInt(map, "rarity"),
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
            updatedAtTicks = GetLong(map, "updatedAtTicks"),
        };

        if (map.TryGetValue("weapons", out var wObj) && wObj is List<object> wList)
            foreach (var w in wList)
                if (w is Dictionary<string, object> wMap)
                    dto.weapons.Add(new WeaponTierRef { name = GetString(wMap, "name"), tier = GetInt(wMap, "tier") });

        if (map.TryGetValue("skills", out var sObj) && sObj is List<object> sList)
            foreach (var s in sList)
                if (s is Dictionary<string, object> sMap)
                    dto.skills.Add(new SkillTierRef { name = GetString(sMap, "name"), tier = GetInt(sMap, "tier") });

        if (map.TryGetValue("pets", out var pObj) && pObj is List<object> pList)
            foreach (var p in pList)
                if (p is Dictionary<string, object> pMap)
                    dto.pets.Add(new PetTierRef { type = GetString(pMap, "type"), tier = GetInt(pMap, "tier") });

        return dto;
    }

    // Helpers de leitura defensivos - valores vindos do Firestore chegam como `object` e o tipo
    // numerico concreto (long/double/int) pode variar; System.Convert lida com qualquer um deles
    // sem lançar por causa de unboxing incompatível.
    private static string GetString(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) ? v as string : null;

    private static bool GetBool(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v is bool b && b;

    private static int GetInt(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToInt32(v) : 0;

    private static long GetLong(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToInt64(v) : 0L;

    private static float GetFloat(Dictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var v) && v != null ? System.Convert.ToSingle(v) : 0f;
}
