using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Aplica os odds (% de chance de sorteio) de Skills/Armas/Pets vindos da tabela original My
// Brute/eternaltwin, mapeada pros nomes finais do AutoArms (pedido do usuário, 2026-07-21) —
// preenche SkillData.odds (campo novo, ver SkillData.cs), WeaponData.dropOdds (já existia, nunca
// preenchido de verdade — só reservado) e PetData.odds (já existia, reservado — ver CLAUDE.md
// Fase 3 "Pets T2/T3"). Idempotente — pode rodar de novo sem problema, só reescreve os mesmos
// valores.
//
// NÃO conecta odds em nenhum sorteio de verdade (CombatSimulator/CombatPlayer/CombatResultPanel.
// ShowLevelUpChoice continuam exatamente como estavam, pesos 60/30/10 de sempre) — pedido
// explícito do usuário nesta tarefa: só preencher o dado, não mexer em como ele é consumido.
//
// Soma das 3 tabelas combinadas = 99.35% (não 100%) — verificado e confirmado batendo exatamente
// com o valor avisado pelo usuário (vem da fonte original My Brute/eternaltwin); NÃO normalizado.
public static class OddsApplier
{
    private const string SkillsFolder = "Assets/ScriptableObjects/Skills";
    private const string WeaponsFolder = "Assets/Data/Weapons";
    private const string PetsFolder = "Assets/ScriptableObjects/Pets";

    // Chave = SkillData.skillName exato do asset (não o nome da tabela original quando os dois
    // divergem) — 3 divergências confirmadas antes de aplicar (2026-07-21):
    //   "Immortality" (tabela) -> asset "Immortal"
    //   "Weapons Master" (tabela) -> asset "Weapon Master"
    //   "Armor" (tabela) -> asset "Armour"
    // "Bandage" e "Backup" da tabela NÃO têm SkillData correspondente no projeto ainda — ficam de
    // fora do dicionário de propósito; ApplyOdds() reporta isso no log em vez de falhar em
    // silêncio.
    private static readonly Dictionary<string, float> SkillOdds = new Dictionary<string, float>
    {
        { "Herculean Strength", 5.76f },
        { "Feline Agility", 5.76f },
        { "Lightning Bolt", 5.76f },
        { "Vitality", 5.76f },
        { "Immortal", 0.01f },
        { "Reconnaissance", 0.10f },
        { "Deity", 0.19f },
        { "Weapon Master", 0.96f },
        { "Martial Arts", 0.96f },
        { "Sixth Sense", 1.92f },
        { "Hostility", 0.38f },
        { "Fists of Fury", 0.96f },
        { "Shield", 0.96f },
        { "Armour", 0.38f },
        { "Toughened Skin", 2.88f },
        { "Untouchable", 0.10f },
        { "Sabotage", 0.29f },
        { "Shock", 0.38f },
        { "Bodybuilder", 0.48f },
        { "Relentless", 0.38f },
        { "Survival", 0.38f },
        { "Lead Skeleton", 0.38f },
        { "Ballet Shoes", 0.38f },
        { "Determination", 0.38f },
        { "Repulse", 0.96f },
        { "Mimic", 0.48f },
        { "First Strike", 0.77f },
        { "Resistant", 0.29f },
        { "Counter Attack", 0.96f },
        { "Iron Head", 0.38f },
        { "Thief", 0.24f },
        { "Fierce Brute", 1.92f },
        { "Tragic Potion", 0.77f },
        { "Net", 1.54f },
        { "Bomb", 0.58f },
        { "Piledriver", 0.10f },
        { "Cry of the Damned", 0.38f },
        { "Hypnosis", 0.05f },
        { "Flash Flood", 0.05f },
        { "Tamer", 0.38f },
        { "Chef", 0.10f },
        { "Spy", 0.29f },
        { "Saboteur", 0.29f },
        { "Hideaway", 0.48f },
        { "Monk", 0.48f },
        { "Vampirism", 0.96f },
        { "Chaining", 0.48f },
        { "Haste", 0.48f },
        { "Treat", 1.92f },
        { "Fast Metabolism", 0.48f },
        { "Sticky Hands", 0.48f },
    };

    // Chave = família da arma (weaponName sem o sufixo " T1"/" T2"/" T3") — 2 divergências de
    // nome confirmadas antes de aplicar: "Bootle" (tabela, typo) -> asset "Bottle"; "Âncora"
    // (tabela, em português) -> asset "Anchor" (o asset é em inglês).
    private static readonly Dictionary<string, float> WeaponOdds = new Dictionary<string, float>
    {
        { "Knife", 7.69f },
        { "Broadsword", 9.61f },
        { "Scimitar", 0.58f },
        { "Axe", 3.84f },
        { "Bumps", 4.80f },
        { "Baton", 6.73f },
        { "Lance", 3.84f },
        { "Hammer", 0.29f },
        { "Whip", 0.29f },
        { "Reaper", 0.19f },
        { "Frying Pan", 0.04f },
        { "Branch", 0.04f },
        { "Book", 0.38f },
        { "Trident", 0.96f },
        { "Morning Star", 0.58f },
        { "Bone", 1.92f },
        { "Fan", 0.19f },
        { "Flail", 0.38f },
        { "Boomerang", 0.04f },
        { "Shuriken", 0.77f },
        { "Bottle", 0.04f },
        { "Racquet", 0.04f },
        { "Sai", 0.58f },
        { "Bow", 0.04f },
        { "Anchor", 0.04f },
        { "Sword", 0.38f },
    };

    // Chave = PetType (enum) — Rato=Mouse, Macaco=Monkey, Javali=Boar.
    private static readonly Dictionary<PetType, float> PetOdds = new Dictionary<PetType, float>
    {
        { PetType.Mouse, 1.92f },
        { PetType.Monkey, 0.10f },
        { PetType.Boar, 0.10f },
    };

    [MenuItem("Tools/AutoArms/Apply My Brute Odds (Skills, Armas, Pets)")]
    public static void ApplyOdds()
    {
        var matchedSkillNames = new HashSet<string>();
        int skillAssetsUpdated = ApplySkillOdds(matchedSkillNames);

        var matchedWeaponNames = new HashSet<string>();
        int weaponAssetsUpdated = ApplyWeaponOdds(matchedWeaponNames);

        var matchedPetTypes = new HashSet<PetType>();
        int petAssetsUpdated = ApplyPetOdds(matchedPetTypes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var unmatchedSkills = new List<string>();
        foreach (var kv in SkillOdds) if (!matchedSkillNames.Contains(kv.Key)) unmatchedSkills.Add(kv.Key);
        var unmatchedWeapons = new List<string>();
        foreach (var kv in WeaponOdds) if (!matchedWeaponNames.Contains(kv.Key)) unmatchedWeapons.Add(kv.Key);
        var unmatchedPets = new List<string>();
        foreach (var kv in PetOdds) if (!matchedPetTypes.Contains(kv.Key)) unmatchedPets.Add(kv.Key.ToString());

        Debug.Log($"[OddsApplier] Skills: {skillAssetsUpdated} assets atualizados ({matchedSkillNames.Count}/{SkillOdds.Count} nomes da tabela encontrados, todos os tiers T1-T3 de cada família). " +
            $"Armas: {weaponAssetsUpdated} assets atualizados ({matchedWeaponNames.Count}/{WeaponOdds.Count} famílias encontradas). " +
            $"Pets: {petAssetsUpdated} assets atualizados ({matchedPetTypes.Count}/{PetOdds.Count} tipos encontrados).");

        if (unmatchedSkills.Count > 0)
            Debug.LogWarning($"[OddsApplier] Skills da tabela SEM SkillData.skillName correspondente no projeto: {string.Join(", ", unmatchedSkills)}. " +
                "(Bandage/Backup não têm asset criado ainda — esperado, ver changelog.)");
        if (unmatchedWeapons.Count > 0)
            Debug.LogWarning($"[OddsApplier] Armas da tabela SEM WeaponData correspondente: {string.Join(", ", unmatchedWeapons)}.");
        if (unmatchedPets.Count > 0)
            Debug.LogWarning($"[OddsApplier] Pets da tabela SEM PetData correspondente: {string.Join(", ", unmatchedPets)}.");
    }

    // Aplica o MESMO odds em TODOS os tiers (T1/T2/T3) de cada skill/arma — odds representa a
    // chance da FAMÍLIA aparecer, não varia por tier (mesmo espírito de description/effectText,
    // que SkillTierGenerator.CopyLiteral já copia verbatim de T1 pra T2/T3).
    private static int ApplySkillOdds(HashSet<string> matchedNames)
    {
        int updated = 0;
        var guids = AssetDatabase.FindAssets("t:SkillData", new[] { SkillsFolder });
        foreach (var guid in guids)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || string.IsNullOrEmpty(skill.skillName)) continue;
            if (!SkillOdds.TryGetValue(skill.skillName, out float odds)) continue;

            skill.odds = odds;
            EditorUtility.SetDirty(skill);
            matchedNames.Add(skill.skillName);
            updated++;
        }
        return updated;
    }

    private static int ApplyWeaponOdds(HashSet<string> matchedNames)
    {
        int updated = 0;
        var guids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsFolder });
        foreach (var guid in guids)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (weapon == null || string.IsNullOrEmpty(weapon.weaponName)) continue;

            string family = StripTierSuffix(weapon.weaponName);
            if (!WeaponOdds.TryGetValue(family, out float odds)) continue;

            weapon.dropOdds = odds;
            EditorUtility.SetDirty(weapon);
            matchedNames.Add(family);
            updated++;
        }
        return updated;
    }

    private static int ApplyPetOdds(HashSet<PetType> matchedTypes)
    {
        int updated = 0;
        var guids = AssetDatabase.FindAssets("t:PetData", new[] { PetsFolder });
        foreach (var guid in guids)
        {
            var pet = AssetDatabase.LoadAssetAtPath<PetData>(AssetDatabase.GUIDToAssetPath(guid));
            if (pet == null) continue;
            if (!PetOdds.TryGetValue(pet.petType, out float odds)) continue;

            pet.odds = odds;
            EditorUtility.SetDirty(pet);
            matchedTypes.Add(pet.petType);
            updated++;
        }
        return updated;
    }

    // Mesmo padrão de WeaponTierGenerator (remove " T1"/" T2"/" T3" do weaponName pra achar a
    // família) — o campo já vem com o sufixo embutido, ex: "Knife T1".
    private static string StripTierSuffix(string name)
    {
        if (name.EndsWith(" T1") || name.EndsWith(" T2") || name.EndsWith(" T3"))
            return name[..^3];
        return name;
    }
}
