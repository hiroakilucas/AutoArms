using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class SkillAssetGenerator
{
    private struct SkillDef
    {
        public string fileName;
        // Nome do arquivo de ícone em Assets/Data/UI/Skills/, se diferente de fileName (ex:
        // o asset é skill_immortal.asset mas o ícone re-adicionado pelo usuário se chama
        // skill_immortality.png, seguindo o nome da skill na lista mestre original). Null/vazio
        // usa fileName como antes.
        public string iconFileName;
        public string skillName;
        public string description;
        public SkillCategory category;
        public SkillActivationType activationType;
        public int usesPerFight;
    }

    private static readonly SkillDef[] Defs = new SkillDef[]
    {
        // CombatPassive
        new SkillDef { fileName = "skill_relentless",         skillName = "Relentless",          description = "+30% accuracy (reduz a esquiva do adversário)",             category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_counter_attack",     skillName = "Counter Attack",       description = "+10% block, +90% reversal após bloquear",                   category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_impact",             skillName = "Impact",               description = "+15% chance de desarmar no golpe",                          category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_pugnacious",         skillName = "Pugnacious",           description = "Chance de contra-atacar após levar dano",                   category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sixth_sense",        skillName = "Sixth Sense",          description = "+10% chance de cancelar o hit do oponente antes de conectar",   category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_monk",               skillName = "Monk",                 description = "+40% chance de cancelar o hit do oponente antes de conectar, -200 iniciativa, nunca ataca (guarda)", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_iron_head",          skillName = "Iron Head",            description = "+40% chance de derrubar a arma do atacante ao sofrer um hit",  category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_shock",              skillName = "Shock",                description = "+50% chance de desarmar o adversário a cada ataque",        category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sabotage",           skillName = "Sabotage",             description = "+50% chance de destruir uma arma do HUD do adversário a cada golpe acertado",  category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_saboteur",           skillName = "Saboteur",             description = "Antes da luta, destrói 1 arma aleatória do oponente e dá -100 iniciativa nele", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_thief",              skillName = "Thief",                description = "44% por turno (desarmado, oponente armado) de roubar a arma dele — até 2x por luta", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 2 },
        new SkillDef { fileName = "skill_untouchable",        skillName = "Untouchable",          description = "+30% chance de esquiva",                                     category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_first_strike",       skillName = "First Strike",         description = "Ataca primeiro independente da velocidade",                 category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_determination",      skillName = "Determination",        description = "Se o golpe não causa dano, 60% de chance de atacar de novo (recursivo)", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_chaining",            skillName = "Chaining",             description = "3 golpes consecutivos sem tomar dano estunam o adversário por 1 ação",   category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // DefensePassive
        new SkillDef { fileName = "skill_shield",             skillName = "Shield",               description = "+45% block rate",                                           category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_armour",             iconFileName = "skill_armor",       skillName = "Armour",               description = "+25% armor, -15% velocidade",                               category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_iron_skin",          skillName = "Iron Skin",            description = "Reduz dano fixo por hit",                                   category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_lead_skeleton",      skillName = "Lead Skeleton",        description = "+15% armor, -15% evasion, -15% dano de arma blunt (Heavy)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_extra_thick_skin",   skillName = "Extra Thick Skin",     description = "Reduz % dano recebido (versão mais forte do Armour)",        category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_toughened_skin",     skillName = "Toughened Skin",       description = "+10% armor",                                                category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_survival",           skillName = "Survival",             description = "Sobrevive com 1 HP uma vez por luta; com 1 HP, +20% block e +20% evasion",  category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_ballet_shoes",       skillName = "Ballet Shoes",         description = "Pula para trás no início da luta",                          category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_resistant",          skillName = "Resistant",            description = "Nenhum hit isolado reduz mais que 25% da vida máxima",       category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sticky_hands",       skillName = "Sticky Hands",         description = "-50% chance de ser desarmado, -50% chance de arremesso (próprio)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // StatBoost
        new SkillDef { fileName = "skill_vitality",           skillName = "Vitality",             description = "+18 HP permanente, +50% HP",                                category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_herculean_strength", skillName = "Herculean Strength",   description = "+3 STR permanente, +50% STR",                               category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_feline_agility",     skillName = "Feline Agility",       description = "+3 AGI permanente, +50% AGI",                               category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_lightning_bolt",     skillName = "Lightning Bolt",       description = "+3 SPD permanente, +50% SPD",                               category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_reconnaissance",     skillName = "Reconnaissance",       description = "-200 iniciativa, +5 SPD permanente, +150% SPD, +50% dano crítico", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_immortal",           iconFileName = "skill_immortality", skillName = "Immortal",             description = "+250% vida, -25% força/agilidade/velocidade",                                     category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_deity",              skillName = "Deity",                description = "+100% HP/STR, -100% AGI/evasão, -90% SPD, -200 iniciativa, +40% reversal", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // WeaponPassive
        new SkillDef { fileName = "skill_weapon_master",      skillName = "Weapon Master",        description = "+50% dano com arma afiada (tag Sharp)",                  category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_strong_arm",         skillName = "Strong Arm",           description = "+dano com armas Heavy",                                     category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_master_of_arms",     skillName = "Master of Arms",       description = "+dano com armas Melee",                                     category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_hostility",          skillName = "Hostility",            description = "+30% reversal",                                              category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_weapon_tampering",   skillName = "Weapon Tampering",     description = "Reduz dano das armas inimigas",                             category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_fists_of_fury",      skillName = "Fists of Fury",        description = "+20% chance de combo",                                       category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_tamer",              skillName = "Tamer",                description = "Pets mais fortes e com mais HP",                            category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_martial_arts",       skillName = "Martial Arts",         description = "+100% dano desarmado",                                      category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_bodybuilder",        skillName = "Bodybuilder",          description = "+10% dexterity (evasion) e +40% hit speed, só com arma Heavy", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_hideaway",           skillName = "Hideaway",             description = "50% chance de arremesso (fixa), +25% bloqueio contra arremessos recebidos, arma volta pro loadout (não desaparece)", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_spy",                skillName = "Spy",                  description = "Antes da luta, metade das armas (aleatórias) do oponente recebem -20% dano permanente", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // Super (Active)
        new SkillDef { fileName = "skill_fierce_brute",       skillName = "Fierce Brute",         description = "Dano duplo no próximo hit (1x por luta)",                   category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_tragic_potion",      skillName = "Tragic Potion",        description = "Recupera HP (1x por luta)",                                 category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_hammer",             skillName = "Hammer",               description = "Golpe massivo de dano (1x por luta)",                       category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_flash_flood",        skillName = "Flash Flood",          description = "17% por ação: com 3+ armas no inventário, arremessa 3 aleatórias que sempre acertam (1x por luta)", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_haste",              skillName = "Haste",                description = "23% por turno: dash que atravessa o oponente, dano baseado em Speed, +5% crítico (1x por luta)", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_piledriver",         skillName = "Piledriver",           description = "17% por turno: agarra e cai sobre o oponente, dano baseado na STR dele, nunca esquivado/bloqueado (1x por luta)", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_net",                skillName = "Net",                  description = "50% por turno: imobiliza o oponente sem dano, sempre acerta — ele só se solta ao sofrer um hit (1x por luta)", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_hypnosis",           skillName = "Hypnosis",             description = "Adversário ataca a si mesmo (1x por luta)",                 category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_bomb",               skillName = "Bomb",                 description = "Explosão de dano alto (1x por luta)",                       category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_cry_of_the_damned",  skillName = "Cry of the Damned",    description = "Reduz stats do adversário (1x por luta)",                   category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
    };

    [MenuItem("Tools/AutoArms/Generate Skill Assets")]
    public static void GenerateAll()
    {
        const string folder = "Assets/ScriptableObjects/Skills";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Skills");

        var allSkills = new List<SkillData>();

        foreach (var def in Defs)
        {
            string assetPath = $"{folder}/{def.fileName}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillData>();
                AssetDatabase.CreateAsset(skill, assetPath);
            }

            skill.skillName      = def.skillName;
            skill.description    = def.description;
            skill.category       = def.category;
            skill.activationType = def.activationType;
            skill.usesPerFight   = def.usesPerFight;

            // Ensure PNG is imported as Sprite then load it
            string iconFile = string.IsNullOrEmpty(def.iconFileName) ? def.fileName : def.iconFileName;
            string iconPath = $"Assets/Data/UI/Skills/{iconFile}.png";
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType     = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            }
            skill.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            EditorUtility.SetDirty(skill);
            allSkills.Add(skill);
        }

        // Create or update SkillDatabase
        string dbPath = $"{folder}/SkillDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(dbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(db, dbPath);
        }
        db.skills = allSkills;
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SkillAssetGenerator] {allSkills.Count} skill assets + SkillDatabase criados em {folder}");
    }
}
