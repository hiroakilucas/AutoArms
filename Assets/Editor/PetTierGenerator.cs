using UnityEngine;
using UnityEditor;

// Espelha SkillTierGenerator.cs, mas pra pets: gera pet_<nome>_t1/t2/t3.asset com os valores
// EXATOS da tabela aprovada pelo usuário (2026-07-16, extraída manualmente da referência visual
// do My Brute e adaptada pro nosso jogo) — valores literais por tier, não multiplicador.
// hpMalusPercent/odds/initiative/comboDebuff/blockDebuff são FIXOS por tipo de pet (mesmo valor
// nos 3 tiers, conforme confirmado pelo usuário) — só str/agility/speed/hp/damage e os bônus
// especiais (comboRate/evasionBase/accuracyBonus/disarmRate) escalam por tier.
public static class PetTierGenerator
{
    private const string PetsFolder = "Assets/ScriptableObjects/Pets";

    // Ícones "de skill" (2026-07-16, fornecidos pelo usuário — mesmo estilo visual dos ícones de
    // skill/arma) — mesmo em todos os 3 tiers de cada pet (diferente de skill/arma, que herdam
    // por previousTier quando T2/T3 não tem sprite próprio: aqui sempre atribuímos direto, já
    // que não existe/faz sentido um ícone diferente por tier pro mesmo bicho).
    private static readonly System.Collections.Generic.Dictionary<PetType, string> IconPaths = new()
    {
        { PetType.Mouse,  "Assets/Data/UI/Pets/Mouse/Rato_Icon.png" },
        { PetType.Monkey, "Assets/Data/UI/Pets/Monkey/Macaco_Icon.png" },
        { PetType.Boar,   "Assets/Data/UI/Pets/Boar/Javali_Icon.png" },
    };

    private struct PetTierValues
    {
        public PetType type;
        public float hpMalusPercent, odds, initiative;   // fixos, mesmo valor nos 3 tiers
        public float comboDebuff, blockDebuff;           // fixos, só Javali (0 pros outros 2)
        public float[] str;                              // [3] (T1,T2,T3)
        public int[] agility, speed, hp, damage;         // [3] cada
        public float[] comboRate, evasionBase, accuracyBonus, disarmRate; // [3] cada

        public PetTierValues(PetType type, float hpMalusPercent, float odds, float initiative,
            float[] str, int[] agility, int[] speed, int[] hp, int[] damage,
            float[] comboRate, float[] evasionBase, float[] accuracyBonus, float[] disarmRate,
            float comboDebuff = 0f, float blockDebuff = 0f)
        {
            this.type = type;
            this.hpMalusPercent = hpMalusPercent; this.odds = odds; this.initiative = initiative;
            this.str = str; this.agility = agility; this.speed = speed; this.hp = hp; this.damage = damage;
            this.comboRate = comboRate; this.evasionBase = evasionBase;
            this.accuracyBonus = accuracyBonus; this.disarmRate = disarmRate;
            this.comboDebuff = comboDebuff; this.blockDebuff = blockDebuff;
        }
    }

    // Tabela aprovada pelo usuário (2026-07-16) — ver CLAUDE.md/CHANGELOG.md pro histórico da
    // conversa. Zero (0f/0) em qualquer slot = pet não usa esse bônus (ex: Rato não tem
    // evasionBase/accuracyBonus/disarmRate; Javali não tem comboRate próprio, só o comboDebuff
    // negativo que penaliza o OPONENTE).
    private static readonly PetTierValues[] Values = new PetTierValues[]
    {
        // RATO (Mouse / Dog) — foco em Combo próprio.
        new PetTierValues(
            type: PetType.Mouse, hpMalusPercent: 0.10f, odds: 0.0192f, initiative: 0f,
            str:        new[] { 7f, 9f, 11f },
            agility:    new[] { 6, 8, 10 },
            speed:      new[] { 5, 7, 9 },
            hp:         new[] { 21, 23, 25 },
            damage:     new[] { 3, 6, 9 },
            comboRate:      new[] { 0.20f, 0.30f, 0.40f },
            evasionBase:    new[] { 0f, 0f, 0f },
            accuracyBonus:  new[] { 0f, 0f, 0f },
            disarmRate:     new[] { 0f, 0f, 0f }
        ),
        // MACACO (Monkey / Panther) — evasivo, Combo próprio ainda maior que o Rato.
        new PetTierValues(
            type: PetType.Monkey, hpMalusPercent: 0.25f, odds: 0.0010f, initiative: 1f,
            str:        new[] { 24f, 29f, 34f },
            agility:    new[] { 17, 21, 25 },
            speed:      new[] { 25, 29, 33 },
            hp:         new[] { 34, 38, 42 },
            damage:     new[] { 3, 6, 9 },
            comboRate:      new[] { 0.70f, 0.75f, 0.80f },
            evasionBase:    new[] { 0.20f, 0.25f, 0.30f },
            accuracyBonus:  new[] { 0f, 0f, 0f },
            disarmRate:     new[] { 0f, 0f, 0f }
        ),
        // JAVALI (Boar / Bear) — tanque disruptivo: buffs próprios (evasão/precisão/desarme) +
        // debuffs FIXOS no oponente (combo/block), sem combo próprio nenhum.
        new PetTierValues(
            type: PetType.Boar, hpMalusPercent: 0.40f, odds: 0.0010f, initiative: 4f,
            str:        new[] { 46f, 51f, 56f },
            agility:    new[] { 3, 5, 7 },
            speed:      new[] { 2, 4, 6 },
            hp:         new[] { 140, 150, 160 },
            damage:     new[] { 5, 10, 15 },
            comboRate:      new[] { 0f, 0f, 0f },
            evasionBase:    new[] { 0.10f, 0.15f, 0.20f },
            accuracyBonus:  new[] { 0.20f, 0.30f, 0.40f },
            disarmRate:     new[] { 0.05f, 0.10f, 0.15f },
            comboDebuff: -0.20f, blockDebuff: -0.25f
        ),
    };

    private const string DatabasePath = "Assets/Resources/PetDatabase.asset";

    [MenuItem("Tools/AutoArms/Generate Pet Tiers (T1, T2 & T3)")]
    public static void GeneratePetTiers()
    {
        if (!AssetDatabase.IsValidFolder(PetsFolder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Pets");

        var t1Assets = new System.Collections.Generic.List<PetData>();
        int created = 0, updated = 0;
        foreach (var v in Values)
        {
            string baseName = v.type.ToString().ToLowerInvariant();

            var t1 = GetOrCreateAsset(baseName, 1, out bool t1New);
            AssignCommon(t1, v, tierIndex: 0, tier: 1, prevTier: null);
            if (t1New) created++; else updated++;

            var t2 = GetOrCreateAsset(baseName, 2, out bool t2New);
            AssignCommon(t2, v, tierIndex: 1, tier: 2, prevTier: t1);
            t1.nextTier = t2;
            if (t2New) created++; else updated++;

            var t3 = GetOrCreateAsset(baseName, 3, out bool t3New);
            AssignCommon(t3, v, tierIndex: 2, tier: 3, prevTier: t2);
            t2.nextTier = t3;
            if (t3New) created++; else updated++;

            EditorUtility.SetDirty(t1);
            EditorUtility.SetDirty(t2);
            EditorUtility.SetDirty(t3);
            t1Assets.Add(t1);
        }

        // PetDatabase.asset (Assets/Resources/, mesmo motivo de WeaponDatabase/SkillDatabase —
        // PlayerProfileConverter precisa resolvê-la via Resources.Load em runtime pro save/load
        // de pets funcionar) — populado automaticamente aqui, só 3 pets no total, sem curadoria
        // manual necessária (diferente de Skill/Weapon, que têm dezenas de famílias).
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var db = AssetDatabase.LoadAssetAtPath<PetDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<PetDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        db.pets = t1Assets;
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PetTierGenerator] {created} assets criados, {updated} atualizados (3 pets × 3 tiers) — PetDatabase.asset atualizado.");
    }

    private static void AssignCommon(PetData dest, PetTierValues v, int tierIndex, int tier, PetData prevTier)
    {
        dest.petType = v.type;
        dest.tier = tier;
        dest.previousTier = prevTier;

        dest.icon = IconPaths.TryGetValue(v.type, out var iconPath) ? AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) : null;

        dest.hpMalusPercent = v.hpMalusPercent;
        dest.odds = v.odds;
        dest.initiative = v.initiative;
        dest.comboDebuff = v.comboDebuff;
        dest.blockDebuff = v.blockDebuff;

        dest.str      = v.str[tierIndex];
        dest.agility  = v.agility[tierIndex];
        dest.speed    = v.speed[tierIndex];
        dest.hp       = v.hp[tierIndex];
        dest.damage   = v.damage[tierIndex];

        dest.comboRate     = v.comboRate[tierIndex];
        dest.evasionBase   = v.evasionBase[tierIndex];
        dest.accuracyBonus = v.accuracyBonus[tierIndex];
        dest.disarmRate    = v.disarmRate[tierIndex];
    }

    private static PetData GetOrCreateAsset(string baseName, int tier, out bool wasCreated)
    {
        string path = $"{PetsFolder}/pet_{baseName}_t{tier}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<PetData>(path);
        if (existing != null) { wasCreated = false; return existing; }

        var p = ScriptableObject.CreateInstance<PetData>();
        AssetDatabase.CreateAsset(p, path);
        wasCreated = true;
        return p;
    }
}
