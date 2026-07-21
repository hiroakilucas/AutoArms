using System.Collections.Generic;
using UnityEngine;

// Lógica pura (sem UI/MonoBehaviour) do sorteio/aplicação de bônus de level-up — extraída de
// CombatResultPanel (DrawOption/SameOption/ApplyBonus/filtros de elegibilidade de
// ShowLevelUpChoice) pra ser reaproveitada também por BotProfileGenerator, sem duplicar os pesos e
// regras de tier-chain em dois lugares. CombatResultPanel continua dono da persistência
// (EditorUtility.SetDirty/LocalSaveService.Save) e da UI — este arquivo só faz a matemática.
public static class LevelUpEngine
{
    // Pool de pets (2026-07-16: T1 dos 3 PetData, wireados em AttackSequencer.petPool — mesmo
    // padrão de skillDatabase/allWeapons). Ainda SEM restrição de duplicatas nem filtro de
    // elegibilidade (isso é a próxima sub-fase — evoluir em vez de somar cópia); por enquanto
    // `pets` é sempre a lista completa de T1, igual ao antigo PetPool fixo, só que vindo de
    // assets em vez de um array de enum embutido no código.
    public static LevelUpOption DrawOption(List<SkillData> skills, List<WeaponData> weapons, List<PetData> pets)
    {
        float wAttr   = 0.60f;
        float wSkill  = skills.Count  > 0 ? 0.30f : 0f;
        float wWeapon = weapons.Count > 0 ? 0.10f : 0f;
        float wPet    = pets.Count    > 0 ? 0.10f : 0f;
        float total   = wAttr + wSkill + wWeapon + wPet;
        float r       = Random.value * total;

        if (r < wAttr)
            return new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = Random.Range(0, 10) };
        if (r < wAttr + wSkill)
            return new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skills[Random.Range(0, skills.Count)] };
        if (r < wAttr + wSkill + wWeapon)
            return new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = weapons[Random.Range(0, weapons.Count)] };
        return new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = pets[Random.Range(0, pets.Count)] };
    }

    // Caixa 1 do level-up (2026-07-21, pedido do usuário): sempre status base (HP/STR/AGI/SPD),
    // nunca skill/arma/pet — mesmo pool de LevelUpOption.Kind.Attribute (attrIndex 0-9) já usado
    // pelo sorteio ponderado de sempre (DrawOption acima), só que sem nenhuma chance de sair outra
    // coisa.
    public static LevelUpOption DrawBaseAttributeOption()
        => new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = Random.Range(0, 10) };

    // Caixas 2+ do level-up (2026-07-21, pedido do usuário): sorteio ponderado pelos odds REAIS já
    // aplicados nos assets (SkillData.odds/WeaponData.dropOdds/PetData.odds, ver OddsApplier) —
    // diferente de DrawOption acima (que usa 3 pesos fixos por TIPO — 60/30/10 — sem olhar o odds
    // individual de cada item). Cada item elegível (já filtrado por BuildAvailableSkills/Weapons/
    // Pets — upgrade de tier, não duplicar o que já possui) entra como uma fatia de
    // `Random.Range(0, 100)` do tamanho do próprio `odds`; a soma das fatias raramente fecha 100
    // (a tabela original My Brute/eternaltwin soma 99.35% no conjunto INTEIRO do jogo, e aqui só
    // uma fração dele costuma estar elegível pra este personagem neste momento) — a fatia residual
    // que sobra (roll caiu depois da última fatia) vira status base (mesmo pool da Caixa 1),
    // nunca uma caixa vazia. Caixas independentes entre si (pedido do usuário) — sem exclusão de
    // item já sorteado numa caixa anterior do mesmo level-up.
    public static LevelUpOption DrawWeightedOption(List<SkillData> skills, List<WeaponData> weapons, List<PetData> pets)
    {
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        if (skills != null)
            foreach (var s in skills)
            {
                if (s == null) continue;
                cumulative += Mathf.Max(0f, s.odds);
                if (roll < cumulative) return new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = s };
            }
        if (weapons != null)
            foreach (var w in weapons)
            {
                if (w == null) continue;
                cumulative += Mathf.Max(0f, w.dropOdds);
                if (roll < cumulative) return new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = w };
            }
        if (pets != null)
            foreach (var p in pets)
            {
                if (p == null) continue;
                cumulative += Mathf.Max(0f, p.odds);
                if (roll < cumulative) return new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = p };
            }

        // Fatia residual — nenhum item elegível cobriu o roll, cai pro status base.
        return DrawBaseAttributeOption();
    }

    public static bool SameOption(LevelUpOption a, LevelUpOption b)
    {
        if (a.kind != b.kind) return false;
        return a.kind switch {
            LevelUpOption.Kind.Attribute => a.attrIndex == b.attrIndex,
            LevelUpOption.Kind.Skill     => a.skill     == b.skill,
            LevelUpOption.Kind.Weapon    => a.weapon    == b.weapon,
            LevelUpOption.Kind.Pet       => a.petData   == b.petData,
            _                            => false
        };
    }

    // Mesma mutação de CombatResultPanel.ApplyBonus, SEM SetDirty/LocalSaveService.Save — quem
    // chama decide se/quando persistir (CombatResultPanel salva pro jogador real; bots nunca
    // salvam, são descartáveis).
    public static void ApplyOption(LevelUpOption opt, PlayerProfile profile)
    {
        switch (opt.kind)
        {
            case LevelUpOption.Kind.Attribute:
                switch (opt.attrIndex)
                {
                    case 0: profile.maxHealth += 12; break;
                    case 1: profile.str       += 2; break;
                    case 2: profile.agility   += 2; break;
                    case 3: profile.speed     += 2; break;
                    case 4: profile.str += 1; profile.agility += 1; break;
                    case 5: profile.agility += 1; profile.speed += 1; break;
                    case 6: profile.str += 1; profile.speed += 1; break;
                    case 7: profile.maxHealth += 6; profile.str     += 1; break;
                    case 8: profile.maxHealth += 6; profile.agility += 1; break;
                    case 9: profile.maxHealth += 6; profile.speed   += 1; break;
                }
                break;
            case LevelUpOption.Kind.Skill:
                if (opt.skill != null && !profile.skills.Contains(opt.skill))
                {
                    // Upgrade de tier: remove o tier anterior da lista antes de adicionar o novo
                    // — mesmo padrão de WeaponData (Kind.Weapon abaixo). Comparação por
                    // nome+tier, mesmo padrão do resto deste arquivo.
                    if (opt.skill.previousTier != null)
                        profile.skills.RemoveAll(ps => ps != null
                            && ps.skillName == opt.skill.previousTier.skillName
                            && ps.tier == opt.skill.previousTier.tier);
                    profile.skills.Add(opt.skill);

                    // Vitality / Herculean Strength / Feline Agility / Lightning Bolt /
                    // Reconnaissance / First Strike: flat permanente (HP/STR/AGI/SPD,
                    // bonusValue2 do asset) aplicado uma única vez na escolha (igual a um pick de
                    // Atributo) — o percentual restante (bonusValue1) é aplicado em runtime sobre
                    // esse valor já somado (ApplySkillStats/GetEffectiveStats).
                    // bonusValue2 é o TOTAL acumulado do tier (ex: Herculean T1=+3, T2=+5 total,
                    // T3=+7 total) — não aditivo por tier. Numa troca de tier o T1/T2 antigo já
                    // aplicou sua parcela, então só a DIFERENÇA entre o total novo e o total do
                    // tier anterior deve ser somada agora (senão dobra a contagem).
                    float prevBonus2 = opt.skill.previousTier != null ? opt.skill.previousTier.bonusValue2 : 0f;
                    int   delta2     = Mathf.RoundToInt(opt.skill.bonusValue2 - prevBonus2);

                    if (opt.skill.skillName == "Vitality")
                        profile.maxHealth += delta2;
                    else if (opt.skill.skillName == "Herculean Strength")
                        profile.str += delta2;
                    else if (opt.skill.skillName == "Feline Agility")
                        profile.agility += delta2;
                    else if (opt.skill.skillName == "Lightning Bolt")
                        profile.speed += delta2;
                    else if (opt.skill.skillName == "Reconnaissance")
                        profile.speed += delta2;
                    else if (opt.skill.skillName == "First Strike")
                        profile.speed += delta2;
                }
                break;
            case LevelUpOption.Kind.Weapon:
                if (opt.weapon != null && profile.weapons != null)
                {
                    // Upgrade de tier: remove o tier anterior do loadout antes de adicionar o novo
                    if (opt.weapon.previousTier != null)
                        profile.weapons.RemoveAll(lw => lw != null && lw.weaponName == opt.weapon.previousTier.weaponName);
                    profile.weapons.Add(opt.weapon);
                }
                break;
            case LevelUpOption.Kind.Pet:
                // Bug real corrigido (2026-07-17, reportado pelo usuário): escolher o mesmo tipo
                // de pet 2x duplicava em vez de evoluir. Upgrade in-place igual skill/arma: se já
                // possui o tier anterior deste pet, remove antes de adicionar o novo — nunca mais
                // de 1 entrada por TIPO de pet no profile (BuildAvailablePets abaixo já garante
                // que só o T1 ou o próximo tier em sequência aparecem como opção, então
                // `opt.petData.previousTier` aqui é sempre exatamente o que está no profile,
                // nunca um tier "pulado").
                if (opt.petData != null)
                {
                    if (opt.petData.previousTier != null)
                        profile.pets.RemoveAll(p => p != null
                            && p.petType == opt.petData.previousTier.petType
                            && p.tier == opt.petData.previousTier.tier);
                    profile.pets.Add(opt.petData);

                    // HP malus é % do HP BASE do dono, cobrado só na aquisição de T1 (confirmado
                    // pelo usuário — evoluir pra T2/T3 não cobra de novo).
                    if (opt.petData.tier == 1)
                    {
                        int malus = Mathf.RoundToInt(profile.maxHealth * opt.petData.hpMalusPercent);
                        profile.maxHealth = Mathf.Max(1, profile.maxHealth - malus);
                    }
                }
                break;
        }
    }

    // Mesma filtragem de ShowLevelUpChoice (T1 só se não tem nenhum tier; T2/T3 só se já tem o
    // tier anterior) — requireIcon preserva o gate de ícone atual da tela real (skills sem ícone
    // re-adicionado em Assets/Data/UI/Skills/ ainda não aparecem pro jogador, ver CLAUDE.md); bots
    // não mostram ícone nenhum, então passam requireIcon:false.
    public static List<SkillData> BuildAvailableSkills(PlayerProfile profile, List<SkillData> allSkills, bool requireIcon)
    {
        var availableSkills = new List<SkillData>();
        if (allSkills == null) return availableSkills;

        foreach (var s in allSkills)
        {
            if (s == null) continue;

            if (requireIcon)
            {
                var rootIcon = s;
                while (rootIcon != null && rootIcon.icon == null) rootIcon = rootIcon.previousTier;
                if (rootIcon == null || rootIcon.icon == null) continue;
            }

            if (s.tier <= 1)
            {
                if (!profile.skills.Exists(ps => ps != null && ps.skillName == s.skillName))
                    availableSkills.Add(s);
            }
            else
            {
                if (s.previousTier != null && profile.skills.Exists(ps =>
                    ps != null && ps.skillName == s.previousTier.skillName && ps.tier == s.previousTier.tier))
                    availableSkills.Add(s);
            }
        }
        return availableSkills;
    }

    public static List<WeaponData> BuildAvailableWeapons(PlayerProfile profile, WeaponData[] allWeaponsPool)
    {
        var availableWeapons = new List<WeaponData>();
        var loadoutWeapons = profile.weapons;
        if (allWeaponsPool == null) return availableWeapons;

        foreach (var w in allWeaponsPool)
        {
            if (w == null) continue;
            if (IsInLoadout(w, loadoutWeapons)) continue;

            if (w.tier <= 1)
            {
                if (!HasUpgradeInLoadout(w, loadoutWeapons, allWeaponsPool))
                    availableWeapons.Add(w);
            }
            else
            {
                if (w.previousTier != null && IsInLoadout(w.previousTier, loadoutWeapons))
                    availableWeapons.Add(w);
            }
        }
        return availableWeapons;
    }

    // Filtro de elegibilidade pra pets (2026-07-17, mesmo espírito de BuildAvailableSkills/
    // BuildAvailableWeapons acima, mas mais simples: só 3 famílias no total, então em vez de
    // varrer um pool grande verificando previousTier, só olha "já tenho esse TIPO?" — se não
    // tem, oferece o T1; se já tem, oferece o `nextTier` (null quando já é T3, não oferece mais
    // nada desse pet). `petPoolT1` é só os 3 T1 (AttackSequencer.petPool/
    // SelectOpponentController.petPool) — T2/T3 são alcançados subindo `nextTier` a partir daqui,
    // nunca precisam estar no pool wireado no Inspector.
    public static List<PetData> BuildAvailablePets(PlayerProfile profile, List<PetData> petPoolT1)
    {
        var available = new List<PetData>();
        if (petPoolT1 == null || profile.pets == null) return available;

        foreach (var t1 in petPoolT1)
        {
            if (t1 == null) continue;
            var owned = profile.pets.Find(p => p != null && p.petType == t1.petType);
            if (owned == null) available.Add(t1);
            else if (owned.nextTier != null) available.Add(owned.nextTier);
        }
        return available;
    }

    // T2/T3 nunca têm icon próprio (ver SkillTierGenerator) — sobe a cadeia previousTier até
    // achar um. Usado pela tela de level-up real (CombatResultPanel) e pelo card de oponente
    // (SelectOpponentController) — sem isso, quase nenhuma skill de tier 2/3 mostraria ícone.
    public static Sprite ResolveSkillIcon(SkillData s)
    {
        while (s != null && s.icon == null) s = s.previousTier;
        return s?.icon;
    }

    // Fallback de sprite pra exibição em UI — mesma cadeia usada por
    // WeaponHandler.EquipSpecific (T2/T3 sem sprite próprio sobem previousTier até achar um).
    // Prioriza `icon` (ícone dedicado de UI); só cai pro `inHandSprite` se nenhum tier da cadeia
    // tiver `icon` configurado.
    public static Sprite ResolveWeaponIcon(WeaponData w)
    {
        var s = w;
        while (s != null && s.icon == null) s = s.previousTier;
        if (s?.icon != null) return s.icon;

        s = w;
        while (s != null && s.inHandSprite == null) s = s.previousTier;
        return s?.inHandSprite;
    }

    public static bool IsInLoadout(WeaponData w, List<WeaponData> loadout)
    {
        if (loadout == null || w == null) return false;
        foreach (var lw in loadout)
            if (lw != null && lw.weaponName == w.weaponName) return true;
        return false;
    }

    // Verifica se alguma versão de tier superior desta arma T1 já está no loadout
    public static bool HasUpgradeInLoadout(WeaponData t1, List<WeaponData> loadout, WeaponData[] allWeapons)
    {
        if (allWeapons == null) return false;
        foreach (var candidate in allWeapons)
        {
            if (candidate == null || candidate.tier <= 1) continue;
            if (candidate.previousTier != null && candidate.previousTier.weaponName == t1.weaponName
                && IsInLoadout(candidate, loadout))
                return true;
            if (candidate.previousTier?.previousTier != null
                && candidate.previousTier.previousTier.weaponName == t1.weaponName
                && IsInLoadout(candidate, loadout))
                return true;
        }
        return false;
    }
}
