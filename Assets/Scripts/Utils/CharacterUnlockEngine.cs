using System.Collections.Generic;
using UnityEngine;

// Unlocks progressivos de skill/arma/pet concedidos ao ganhar um personagem via case opening na
// Loja (2026-07-25, corrigido no mesmo dia — ver nota de correção abaixo) - cada raridade concede
// N sorteios sequenciais (UnlockCountForRarity: Normal 1, Uncommon 2, Rare 3, Legendary 4,
// Immortal 5).
//
// CORREÇÃO DE DESIGN (2026-07-25): a 1ª versão decidia o tier elegível pela POSIÇÃO do unlock na
// sequência (1º só T1, 2º T1/T2, 3º+ T1/T2/T3), independente de qual item específico saía —
// permitia um personagem sair com "Faca T2" sem nunca ter possuído "Faca T1", o que não deveria
// ser possível (reportado pelo usuário: Raro saiu com Vitalidade T1/Faca T2/Rato T3). A regra
// certa é a MESMA já usada no level-up de combate (BuildAvailableSkills/Weapons/Pets — upgrade
// determinístico por POSSE, nunca sorteio de tier por probabilidade): cada unlock sorteia uma
// FAMÍLIA (skill/arma/pet específica, ponderado pelos odds reais de sempre — SkillData.odds/
// WeaponData.dropOdds/PetData.odds, mesma fonte do level-up) e o TIER concedido é sempre
// "1 + o maior tier que o personagem já possui desta família específica" (considerando os
// unlocks já resolvidos NESTA MESMA sequência, já que o personagem começa do zero) — nunca um
// tier arbitrário. T2/T3 só aparecem quando o sorteio calha de repetir a mesma família em
// unlocks diferentes da mesma sequência (raro por natureza, como esperado). Família já no teto
// (T3) não desperdiça o unlock — sorteia de novo até cair em algo que ainda pode subir de tier
// ou em item novo.
//
// Reaproveita LevelUpOption/LevelUpEngine.ApplyOption pra aplicar o resultado (mesma mutação de
// profile.skills/weapons/pets, upgrade de tier in-place, bônus de stat flat de Vitality/
// Herculean Strength/etc, HP malus de pet na 1ª aquisição - tudo já testado no level-up de
// combate, sem duplicar lógica aqui).
public static class CharacterUnlockEngine
{
    public static int UnlockCountForRarity(CharacterRarity rarity) => rarity switch
    {
        CharacterRarity.Normal    => 1,
        CharacterRarity.Uncommon  => 2,
        CharacterRarity.Rare      => 3,
        CharacterRarity.Legendary => 4,
        CharacterRarity.Immortal  => 5,
        _                         => 1,
    };

    // Tentativas de sorteio de família por unlock antes de desistir (rede de segurança contra um
    // pool inteiro já no teto — praticamente inatingível: ~50 skills + 26 armas + 3 pets pra no
    // máximo 5 unlocks por personagem).
    private const int MaxRerollAttempts = 500;

    private static SkillDatabase _skillDatabase;
    private static WeaponDatabase _weaponDatabase;
    private static PetDatabase _petDatabase;

    // Mesmo padrão de PlayerProfileConverter (Resources.Load cacheado em runtime) - os 3 databases
    // já existem e já moram em Assets/Resources/ por causa da camada de save.
    private static SkillDatabase GetSkillDatabase()
    {
        if (_skillDatabase == null) _skillDatabase = Resources.Load<SkillDatabase>("SkillDatabase");
        return _skillDatabase;
    }

    private static WeaponDatabase GetWeaponDatabase()
    {
        if (_weaponDatabase == null) _weaponDatabase = Resources.Load<WeaponDatabase>("WeaponDatabase");
        return _weaponDatabase;
    }

    private static PetDatabase GetPetDatabase()
    {
        if (_petDatabase == null) _petDatabase = Resources.Load<PetDatabase>("PetDatabase");
        return _petDatabase;
    }

    // Candidato de sorteio = uma FAMÍLIA (não um tier específico) - guarda só o necessário pra
    // (a) pesar pelo odds e (b) resolver depois, por posse, qual tier conceder.
    private struct FamilyCandidate
    {
        public LevelUpOption.Kind kind;
        public float odds;
        public string skillName;
        public string weaponFamily;
        public PetType petType;
    }

    // profile: já reflete tudo concedido em unlocks ANTERIORES desta mesma sequência (o chamador
    // aplica cada resultado via LevelUpEngine.ApplyOption antes de sortear o próximo) - é a partir
    // dele que se decide se a família sorteada vira T1 (nunca possuiu), T2/T3 (upgrade) ou é
    // redescartada por já estar no teto.
    public static LevelUpOption DrawUnlock(PlayerProfile profile)
    {
        var pool = BuildFamilyPool();
        if (pool.Count == 0) return LevelUpEngine.DrawBaseAttributeOption();

        float total = 0f;
        foreach (var c in pool) total += c.odds;

        for (int attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var picked = WeightedPick(pool, total);
            int ownedTier = OwnedTier(picked, profile);
            if (ownedTier >= 3) continue; // já no teto - não desperdiça o unlock, sorteia de novo

            var option = ResolveOption(picked, ownedTier + 1);
            if (option.HasValue) return option.Value;
            // Dado inconsistente (tier não encontrado no database) - tenta outra família.
        }

        // Rede de segurança (praticamente inatingível) - cai pro mesmo pool de status base do
        // level-up de combate em vez de travar a sequência.
        return LevelUpEngine.DrawBaseAttributeOption();
    }

    private static List<FamilyCandidate> BuildFamilyPool()
    {
        var pool = new List<FamilyCandidate>();

        // SkillDatabase.skills lista TODOS os tiers de toda skill (ver SkillDatabase.cs) - filtra
        // só a raiz (tier==1) de cada família, um candidato por família.
        var skillDb = GetSkillDatabase();
        if (skillDb?.skills != null)
            foreach (var s in skillDb.skills)
            {
                if (s == null || s.tier != 1) continue;
                pool.Add(new FamilyCandidate { kind = LevelUpOption.Kind.Skill, odds = Mathf.Max(0f, s.odds), skillName = s.skillName });
            }

        // WeaponDatabase.weapons já só lista os T1 (raiz de cada família).
        var weaponDb = GetWeaponDatabase();
        if (weaponDb?.weapons != null)
            foreach (var w in weaponDb.weapons)
            {
                if (w == null) continue;
                string family = WeaponNameUtil.StripWeaponTierSuffix(w.weaponName);
                pool.Add(new FamilyCandidate { kind = LevelUpOption.Kind.Weapon, odds = Mathf.Max(0f, w.dropOdds), weaponFamily = family });
            }

        // PetDatabase.pets já só lista os T1.
        var petDb = GetPetDatabase();
        if (petDb?.pets != null)
            foreach (var p in petDb.pets)
            {
                if (p == null) continue;
                pool.Add(new FamilyCandidate { kind = LevelUpOption.Kind.Pet, odds = Mathf.Max(0f, p.odds), petType = p.petType });
            }

        return pool;
    }

    private static FamilyCandidate WeightedPick(List<FamilyCandidate> pool, float total)
    {
        if (total <= 0f) return pool[Random.Range(0, pool.Count)];

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var c in pool)
        {
            cumulative += c.odds;
            if (roll < cumulative) return c;
        }
        return pool[pool.Count - 1]; // segurança contra erro de ponto flutuante
    }

    // Maior tier que o personagem já possui DESTA família específica - 0 = ainda não possui
    // nenhum tier dela. Lê profile.skills/weapons/pets diretamente (já inclui tudo concedido em
    // unlocks anteriores desta mesma sequência - ver comentário de DrawUnlock acima).
    private static int OwnedTier(FamilyCandidate c, PlayerProfile profile)
    {
        int max = 0;
        switch (c.kind)
        {
            case LevelUpOption.Kind.Skill:
                if (profile.skills != null)
                    foreach (var s in profile.skills)
                        if (s != null && s.skillName == c.skillName && s.tier > max) max = s.tier;
                break;
            case LevelUpOption.Kind.Weapon:
                if (profile.weapons != null)
                    foreach (var w in profile.weapons)
                        if (w != null && WeaponNameUtil.StripWeaponTierSuffix(w.weaponName) == c.weaponFamily && w.tier > max) max = w.tier;
                break;
            case LevelUpOption.Kind.Pet:
                if (profile.pets != null)
                    foreach (var p in profile.pets)
                        if (p != null && p.petType == c.petType && p.tier > max) max = p.tier;
                break;
        }
        return max;
    }

    // Resolve o LevelUpOption pro tier decidido (grantTier) - reaproveita os
    // FindByFamilyNameAndTier/FindByTypeAndTier já existentes nos 3 databases (mesma resolução
    // usada pela camada de save, PlayerProfileConverter.ApplyDTO).
    private static LevelUpOption? ResolveOption(FamilyCandidate c, int grantTier)
    {
        switch (c.kind)
        {
            case LevelUpOption.Kind.Skill:
            {
                var skill = GetSkillDatabase()?.FindByFamilyNameAndTier(c.skillName, grantTier);
                return skill != null ? new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skill } : (LevelUpOption?)null;
            }
            case LevelUpOption.Kind.Weapon:
            {
                var weapon = GetWeaponDatabase()?.FindByFamilyNameAndTier(c.weaponFamily, grantTier);
                return weapon != null ? new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = weapon } : (LevelUpOption?)null;
            }
            case LevelUpOption.Kind.Pet:
            {
                var pet = GetPetDatabase()?.FindByTypeAndTier(c.petType, grantTier);
                return pet != null ? new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = pet } : (LevelUpOption?)null;
            }
            default:
                return null;
        }
    }

    // Resolve um resultado vindo do SERVIDOR (UnlockRerollService/rerollUnlock — kind em string
    // "skill"/"weapon"/"pet", nome de família + tier já decididos lá) pro mesmo LevelUpOption
    // usado pelo sorteio local (DrawUnlock acima) — reaproveita os mesmos databases/Find* já
    // usados aqui, já que a function só devolve nome+tier (não uma referência de asset Unity).
    public static LevelUpOption? ResolveServerResult(string kind, string name, int tier)
    {
        switch (kind)
        {
            case "skill":
            {
                var skill = GetSkillDatabase()?.FindByFamilyNameAndTier(name, tier);
                return skill != null ? new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skill } : (LevelUpOption?)null;
            }
            case "weapon":
            {
                var weapon = GetWeaponDatabase()?.FindByFamilyNameAndTier(name, tier);
                return weapon != null ? new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = weapon } : (LevelUpOption?)null;
            }
            case "pet":
            {
                if (!System.Enum.TryParse(name, out PetType petType)) return null;
                var pet = GetPetDatabase()?.FindByTypeAndTier(petType, tier);
                return pet != null ? new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = pet } : (LevelUpOption?)null;
            }
            default:
                return null;
        }
    }

    // Converte um LevelUpOption (sorteio local ou já resolvido de volta de um resultado do
    // servidor) pro mesmo shape "kind/name/tier" que rerollUnlock usa — pra poder PERSISTIR o
    // rascunho atual (PlayerProfile.pendingUnlockKind/Name/Tier) de forma que sobrevive a fechar
    // o app no meio de um reveal (2026-07-25, bug real corrigido: reabrir sorteava um resultado
    // diferente em vez de continuar mostrando o mesmo rascunho).
    public static (string kind, string name, int tier) ToServerShape(LevelUpOption opt)
    {
        switch (opt.kind)
        {
            case LevelUpOption.Kind.Skill:
                return ("skill", opt.skill != null ? opt.skill.skillName : "", opt.skill != null ? opt.skill.tier : 1);
            case LevelUpOption.Kind.Weapon:
                return ("weapon", opt.weapon != null ? WeaponNameUtil.StripWeaponTierSuffix(opt.weapon.weaponName) : "", opt.weapon != null ? opt.weapon.tier : 1);
            case LevelUpOption.Kind.Pet:
                return ("pet", opt.petData != null ? opt.petData.petType.ToString() : "", opt.petData != null ? opt.petData.tier : 1);
            default:
                return ("", "", 0);
        }
    }
}
