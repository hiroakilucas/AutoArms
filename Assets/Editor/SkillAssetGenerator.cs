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
        // Descrição temática/engraçada (skills_descricoes_v2.md) — mostrada no popup de
        // detalhe da skill. Não menciona tier/T1/T2/T3 (a evolução de nível é comunicada só
        // pela borda colorida do ícone).
        public string description;
        // Texto do "Efeito:" (skills_descricoes_v2.md), formato "Label +[v1/v2/v3]%" — os 3
        // valores entre colchetes são os 3 tiers da MESMA skill; o popup destaca o valor do
        // tier equipado (ver CharacterPanel.HighlightEffectTiers). Vazio para Garimpeiro/Magneto
        // (ainda não implementadas — popup não deve mostrar linha de Efeito pra elas).
        public string effectText;
        public SkillCategory category;
        public SkillActivationType activationType;
        public int usesPerFight;
    }

    private static readonly SkillDef[] Defs = new SkillDef[]
    {
        // CombatPassive
        new SkillDef { fileName = "skill_relentless",         skillName = "Relentless",          description = "Ele bate com tanta certeza que a esquiva do adversário simplesmente desiste.", effectText = "Accuracy (reduz esquiva do defensor) +[30/40/50]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_counter_attack",     skillName = "Counter Attack",       description = "Bloqueia com uma mão e já devolve com a outra.", effectText = "Bloqueio +[10/15/20]%, chance de reversão após bloqueio +[90/95/99]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sixth_sense",        skillName = "Sixth Sense",          description = "Ele sente o golpe chegando um segundo antes de acontecer — e já contra-ataca.", effectText = "Counter +[10/15/20]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_monk",               skillName = "Monk",                 description = "Paciência de monge: ele é mais lento pra agir, mas quando contra-ataca, dói.", effectText = "Counter +[40/45/50]%, Iniciativa −200 (fixo)", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_iron_head",          skillName = "Iron Head",            description = "Um cabeçada tão dura que a arma do adversário simplesmente sai voando da mão dele.", effectText = "+[40/50/60]% de chance de derrubar a arma do atacante ao sofrer um hit (interrompe o combo dele)", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_shock",              skillName = "Shock",                description = "Um choque na hora certa e a arma do adversário já não tem mais tanta vontade de ficar na mão dele.", effectText = "Chance de Desarme +[50/60/70]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sabotage",           skillName = "Sabotage",             description = "Ele não ataca o adversário — ataca o inventário dele.", effectText = "[50/75/90]% por golpe acertado: destrói 1 arma aleatória do HUD do adversário", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_saboteur",           skillName = "Saboteur",             description = "A primeira arma que o adversário puxar já nasce quebrada.", effectText = "1ª arma puxada pelo oponente quebra 100% das vezes; Iniciativa do oponente −[100/150/200]", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_thief",              skillName = "Thief",                description = "\"Isso é meu agora\" — mas só quando ele está desarmado e o outro não.", effectText = "44%/turno de chance de roubar arma, até [2/3/4]x por luta", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 2 },
        new SkillDef { fileName = "skill_untouchable",        skillName = "Untouchable",          description = "Intocável não é modéstia, é currículo.", effectText = "Evasion +[30/40/50]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_first_strike",       skillName = "First Strike",         description = "Enquanto o adversário ainda está se ajeitando, ele já terminou o primeiro round.", effectText = "Iniciativa +[200/300/500] — Speed +[0/2/4]", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_determination",      skillName = "Determination",        description = "Falhou uma vez? Ele tenta de novo. E de novo. E de novo.", effectText = "Golpe que falha (esquiva/bloqueio/counter) tem [60/70/80]% de chance fixa de tentar de novo, recursivamente", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_chaining",           skillName = "Chaining",             description = "Três acertos limpos seguidos e o adversário nem viu o desarme chegando.", effectText = "3 hits melee consecutivos sem tomar dano = atordoa o defensor por 1 ação + desarme garantido. Combo +[0/10/20]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_chef",               skillName = "Chef",                 description = "Ele não cozinha pra se alimentar — cozinha pra sabotar o jantar do adversário.", effectText = "1x/luta, na 1ª ação: envenena o oponente com uma \"pizza envenenada\", causando [1.5/3/5]% do HP máximo dele por turno até ele se curar", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_hostility",          skillName = "Hostility",            description = "Ele não gosta de você. Isso, tecnicamente, é uma vantagem em combate.", effectText = "Reversal +[30/35/40]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_fists_of_fury",      skillName = "Fists of Fury",        description = "Um soco puxa o outro, que puxa o outro, sem parar pra respirar.", effectText = "Chance de Combo +[20/30/40]%", category = SkillCategory.CombatPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // DefensePassive
        new SkillDef { fileName = "skill_shield",             skillName = "Shield",               description = "Escudo na medida certa: aguenta o golpe, mas cobra seu preço no dano que ele mesmo causa.", effectText = "Bloqueio +[45/50/55]%, penalidade de Dano −25% enquanto o escudo está ativo (fixo), 10% de chance fixa do escudo cair sozinho (separada do desarme normal) — desarme tem prioridade sobre o escudo", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_armour",             iconFileName = "skill_armor",       skillName = "Armour",               description = "Vestido pra batalha, ao custo de ficar um pouco mais lento.", effectText = "Armor +[25/30/35]%, Speed −15% (fixo, penalidade)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_lead_skeleton",      skillName = "Lead Skeleton",        description = "Ossos de chumbo: pesado de carregar, quase impossível de perfurar com machado.", effectText = "−[15/20/25]% de dano recebido de armas Heavy (após crítico, antes de armor); Armor +[15/25/35]%; Evasion −15% (fixo, penalidade)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_toughened_skin",     skillName = "Toughened Skin",       description = "A pele dele parece ter sido curtida em couro de dragão.", effectText = "Armor +[10/15/20]%", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_survival",           skillName = "Survival",             description = "Instinto de sobrevivência puro: mesmo por um fio, ele luta melhor do que nunca.", effectText = "1x/luta sobrevive com 1 HP; enquanto estiver com 1 HP, ganha Evasion e Bloqueio +[20/30/40]%", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_ballet_shoes",       skillName = "Ballet Shoes",         description = "Leve nos pés — o primeiro golpe da luta nem chega a encostar nele.", effectText = "Evasion +[10/15/20]% + primeiro golpe da luta é sempre esquivado", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_resistant",          skillName = "Resistant",            description = "Não importa o tamanho do golpe, ele nunca é grande demais pra aguentar de uma vez.", effectText = "Nenhum hit isolado pode ultrapassar [25/20/17]% do HP máximo dele (cap no dano bruto, antes de armor/Lead Skeleton)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_sticky_hands",       skillName = "Sticky Hands",         description = "A arma gruda na mão dele como se fossem amigos de infância.", effectText = "Reduz em [50/60/70]% tanto a chance de ser desarmado quanto a própria chance de arremessar a arma", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_fast_metabolism",    skillName = "Fast Metabolism",      description = "Ele se recupera rápido — só que fica um pouco mais lento e menos certeiro por causa disso.", effectText = "Regenera [1/2/3]% de HP por turno; abaixo de 50% HP, dispara um burst único de 10×5% HP (se não tomar dano); Velocidade de Ataque −50% e Crítico −5% (fixos, penalidade)", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_repulse",            skillName = "Repulse",              description = "Devolve o que jogaram nele — com juros.", effectText = "[30/35/40]% de chance de deflectir um arremesso recebido de volta, com +[5/10/15]% de crítico no deflect", category = SkillCategory.DefensePassive, activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // StatBoost
        new SkillDef { fileName = "skill_vitality",           skillName = "Vitality",             description = "Corpo cheio de vida — dá até inveja no meio da arena.", effectText = "+[18/30/42] HP permanente + HP% +[50/60/70]%", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_herculean_strength", skillName = "Herculean Strength",   description = "Forte o bastante pra mover montanhas, mas primeiro é bom derrotar o adversário.", effectText = "+[3/5/7] Strength permanente (acumulativo) + Strength% +[50/60/70]%", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_feline_agility",     skillName = "Feline Agility",       description = "Cai de pé sempre — literalmente, até quando não devia.", effectText = "+[3/5/7] Agility permanente (acumulativo) + Agility% +[50/60/70]%", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_lightning_bolt",     skillName = "Lightning Bolt",       description = "Um raio de verdade seria mais lento que ele nesse dia.", effectText = "+[3/5/7] Speed permanente (acumulativo) + Speed% +[50/60/70]%", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_reconnaissance",     skillName = "Reconnaissance",       description = "Ele já sabia o ponto fraco do adversário antes da luta começar.", effectText = "+[5/10/15] Speed permanente + Speed% +[150/200/250]%, Iniciativa −200 (fixo), Dano Crítico +[50/60/70]%", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_immortal",           iconFileName = "skill_immortality", skillName = "Immortal",             description = "\"Só mais uma vidinha\" — ele quase nunca fica sem elas, mas paga o preço na agilidade.", effectText = "HP% +[250/300/350]%; Strength%/Agility%/Speed% −25% cada (fixo, penalidade)", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_deity",              skillName = "Deity",                description = "Um toque de divindade — maior, mais forte, mais resistente, mas também mais lento e sem esquiva.", effectText = "HP% +[100/125/150]%, Strength% +[100/125/150]%, com penalidades fixas: Agility% −100%, Speed% −90%, Evasion% −100% (sem esquiva), Reversal +[40/50/60]%, Iniciativa −200, +50% de tamanho do personagem", category = SkillCategory.StatBoost,      activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // WeaponPassive
        new SkillDef { fileName = "skill_weapon_master",      skillName = "Weapon Master",        description = "Qualquer arma afiada na mão dele parece ter nascido pra estar ali.", effectText = "+[50/75/100]% de dano com armas Sharp (multiplicador 1.5/1.75/2.0)", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_martial_arts",       skillName = "Martial Arts",         description = "Anos de dojo resumidos em socos e chutes que doem mais que qualquer espada.", effectText = "Dano Desarmado ×[2/2.5/3]", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_bodybuilder",        skillName = "Bodybuilder",          description = "Treino é treino, resultado é resultado — principalmente com uma arma pesada na mão.", effectText = "(só com arma Heavy equipada) Dexterity com Heavy +[10/15/20]%, Velocidade de Ataque com Heavy +[40/50/60]%", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_hideaway",           skillName = "Hideaway",             description = "No primeiro round, ele simplesmente... não está lá pra ser atingido de longe.", effectText = "Chance de arremesso fixa em 50% (substitui a soma normal por tag de arma); Bloqueio contra arremesso do oponente −[25/30/35]%", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_spy",                skillName = "Spy",                  description = "Ele já sabia toda a estratégia do adversário antes do juiz apitar — e sabotou a metade do arsenal dele.", effectText = "Antes da luta, metade das armas do oponente (aleatórias) recebem −[20/25/30]% de dano permanente", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },
        new SkillDef { fileName = "skill_garimpeiro",         skillName = "Garimpeiro",           description = "Sai da arena de mãos vazias, mas nunca deixa uma arma boa largada no chão.", effectText = "", category = SkillCategory.WeaponPassive,  activationType = SkillActivationType.Passive, usesPerFight = 1 },

        // Super (Active)
        new SkillDef { fileName = "skill_fierce_brute",       skillName = "Fierce Brute",         description = "Golpe crítico dele não machuca — devasta.", effectText = "33%/turno (não consome ação): dobra o dano do 1º golpe corpo a corpo + Crítico extra de +[10/20/30]%; usos = [1/2/3] + STR/30", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_tragic_potion",      skillName = "Tragic Potion",        description = "A história por trás dessa poção é triste. O efeito, nem tanto.", effectText = "Com HP < 60%: 50%/turno cura 25-50% do HP máximo, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_flash_flood",        skillName = "Flash Flood",          description = "Uma saraivada de armas que ninguém viu vindo — e todas acertam.", effectText = "17%/ação, com 3+ armas no inventário: arremessa 3 armas aleatórias que sempre acertam, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_haste",              skillName = "Haste",                description = "Ele atravessa o adversário antes que ele perceba que a luta já continuou.", effectText = "23%/turno: dash atravessando o oponente, dano baseado em Speed, +[5/10/15]% de crítico, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_piledriver",         skillName = "Piledriver",           description = "Não é queda, é aterrissagem forçada — com a força do próprio adversário.", effectText = "17%/turno: causa dano usando a Strength do DEFENSOR, nunca é esquivado nem bloqueado, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_net",                skillName = "Net",                  description = "Rede na mão, liberdade do adversário no chão.", effectText = "50%/turno, sempre acerta, sem dano: imobiliza o oponente até ele sofrer 1 hit, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_bomb",               skillName = "Bomb",                 description = "Ele não luta sozinho — traz companhia explosiva, e ninguém escapa dela.", effectText = "17%/turno: 15-25 de dano em área, ignora esquiva/bloqueio/crítico/armor, quebra o efeito de Net, até [2/3/4]x por luta, consome o turno", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 2 },
        new SkillDef { fileName = "skill_vampirism",          skillName = "Vampirism",            description = "Cada mordida é também um gole de energia roubada.", effectText = "Com HP < 50%: 33%/turno, mordida garantida que causa 25% do HP faltante como dano e cura o mesmo valor, até [1/2/3]x por luta, consome o turno", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_mimic",              skillName = "Mimic",                description = "Copia o golpe mais recente do adversário e devolve com juros.", effectText = "Copia a última Super ativa usada pelo oponente — [1x/2x/3x]/luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_magneto",            skillName = "Magneto",              description = "As armas simplesmente preferem flutuar até a mão dele.", effectText = "", category = SkillCategory.Super, activationType = SkillActivationType.Active, usesPerFight = 1 },

        // Relacionadas a Pets
        new SkillDef { fileName = "skill_hypnosis",           skillName = "Hypnosis",             description = "Um olhar e o pet do adversário muda de lado sem nem perceber.", effectText = "38%/turno: hipnotiza um pet inimigo vivo (90% de chance), fazendo-o trocar de time, até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_cry_of_the_damned",  skillName = "Cry of the Damned",    description = "Um grito que gela a espinha de qualquer pet.", effectText = "44%/turno: expulsa cada pet inimigo vivo (50% de chance cada, permanente), até [1/2/3]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 1 },
        new SkillDef { fileName = "skill_tamer",              skillName = "Tamer",                description = "Seu pet luta mais forte só de ouvir a voz dele.", effectText = "Deixa os pets do jogador mais fortes e com mais HP (valores detalhados em PETS.md), até [3/4/5]x por luta", category = SkillCategory.Super,          activationType = SkillActivationType.Active,  usesPerFight = 4 },
        new SkillDef { fileName = "skill_treat",              skillName = "Treat",                description = "Um agrado na hora certa, e o pet vira uma fera na arena.", effectText = "33%/turno: cura o pet aliado mais fraco em 50% do HP + dá um escudo de 1 golpe + força um ataque, até [4/5/6]x por luta", category = SkillCategory.Super, activationType = SkillActivationType.Active, usesPerFight = 4 },
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
            skill.effectText     = def.effectText;
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

        // Inclui os T2/T3 já gerados por SkillTierGenerator (não fazem parte de Defs[], que só
        // lista os T1) — sem isso, rodar esta tool depois de "Rebuild Skill Database From
        // Folder" apagava silenciosamente os tiers do database (bug real: SkillDatabase.asset
        // voltava a ter só os 53 T1, escondendo upgrades T2/T3 do level-up até alguém lembrar de
        // rodar o rebuild de novo).
        var guids = AssetDatabase.FindAssets("t:SkillData", new[] { folder });
        foreach (var guid in guids)
        {
            var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && s.tier > 1) allSkills.Add(s);
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
