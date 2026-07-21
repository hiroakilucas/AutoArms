using System.Collections.Generic;
using UnityEngine;

// Gera oponentes "bot" pra 05_SelectOpponent (fallback quando não há gente real o bastante
// online) — reaproveita a identidade visual de um PlayerProfile já existente (prefab/ícone/
// splash art), mas os stats/skills/armas são gerados em runtime, escalados pro level pedido, em
// vez de fixos no asset. Nunca persiste (sem SetDirty/LocalSaveService.Save) — o bot é descartável,
// recriado a cada visita a 05_SelectOpponent, mesmo espírito de
// PlayerProfileConverter.FromOpponentIndexMap (que já cria PlayerProfile em runtime pra oponentes
// remotos vindos do Firestore).
public static class BotProfileGenerator
{
    public static PlayerProfile Generate(PlayerProfile template, int level,
                                          SkillDatabase skillDatabase, WeaponData[] allWeapons,
                                          PetData[] petPool = null)
    {
        var bot = ScriptableObject.CreateInstance<PlayerProfile>();

        // Identidade visual — copiada do template, nunca alterada.
        bot.profileName    = template.profileName;
        bot.characterId    = "bot_" + template.name;
        bot.characterPrefab = template.characterPrefab;
        bot.previewIcon    = template.previewIcon;
        bot.splashArt      = template.splashArt;
        bot.rarity         = template.rarity;
        bot.attackSettings = template.attackSettings;
        bot.scale          = template.scale;
        bot.startPos       = template.startPos;
        bot.isUnlockedForSelection = false;
        bot.isPlayable             = false;

        // Level 1: mesmos stats base de um personagem novo (55 HP/2 STR/2 AGI/2 SPD + pool de 9
        // pontos aleatórios), mas com 1 skill OU 1 arma garantida — diferente de um personagem
        // normal, que começa com weapons/skills vazios (bot precisa aparecer com pelo menos algo
        // equipado desde o level 1, pedido do usuário).
        var stats = CharacterCreation.GenerateLevel1Stats();
        bot.maxHealth = stats.maxHealth;
        bot.str       = stats.str;
        bot.agility   = stats.agility;
        bot.speed     = stats.speed;
        bot.level     = 1;

        var skillPool  = skillDatabase != null ? skillDatabase.skills : null;
        GrantStartingSkillOrWeapon(bot, skillPool, allWeapons);

        // Level 2 em diante: mesmo bônus que um jogador real recebe a cada level-up — +2 HP
        // automático (XpSystem.ApplyLevelBonus) + 1 escolha sorteada com os mesmos pesos de
        // CombatResultPanel.DrawOption (60% atributo / 30% skill / 10% arma / 10% pet) —
        // acumulado sequencialmente, então armas/skills podem subir de tier no caminho, igual a
        // um jogador que jogou várias lutas de verdade.
        for (int lvl = 2; lvl <= level; lvl++)
        {
            bot.maxHealth += 2;
            bot.level = lvl;

            var availableSkills  = LevelUpEngine.BuildAvailableSkills(bot, skillPool, requireIcon: false);
            var availableWeapons = LevelUpEngine.BuildAvailableWeapons(bot, allWeapons);
            // Bug real corrigido (2026-07-17): recalculado a cada level-up (mesmo padrão de
            // skill/arma acima) — antes era montado 1x fora do loop com o pool inteiro sem
            // filtro, então um bot podia "escolher" o mesmo pet várias vezes e duplicar.
            var availablePets = LevelUpEngine.BuildAvailablePets(bot, petPool != null ? new List<PetData>(petPool) : null);
            var option = LevelUpEngine.DrawOption(availableSkills, availableWeapons, availablePets);
            LevelUpEngine.ApplyOption(option, bot);
        }

        return bot;
    }

    // Sorteia 50/50 entre dar 1 skill ou 1 arma inicial (T1, mesma elegibilidade de
    // ShowLevelUpChoice) — cai pro outro pool se o sorteado vier vazio; sem efeito se os dois
    // pools estiverem vazios (skillDatabase/allWeapons não configurados no Inspector ainda).
    private static void GrantStartingSkillOrWeapon(PlayerProfile bot, List<SkillData> skillPool, WeaponData[] allWeapons)
    {
        var availableSkills  = LevelUpEngine.BuildAvailableSkills(bot, skillPool, requireIcon: false);
        var availableWeapons = LevelUpEngine.BuildAvailableWeapons(bot, allWeapons);

        bool giveSkill = Random.value < 0.5f;
        if (giveSkill && availableSkills.Count > 0)
        {
            var skill = availableSkills[Random.Range(0, availableSkills.Count)];
            LevelUpEngine.ApplyOption(new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skill }, bot);
        }
        else if (availableWeapons.Count > 0)
        {
            var weapon = availableWeapons[Random.Range(0, availableWeapons.Count)];
            LevelUpEngine.ApplyOption(new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = weapon }, bot);
        }
        else if (availableSkills.Count > 0)
        {
            var skill = availableSkills[Random.Range(0, availableSkills.Count)];
            LevelUpEngine.ApplyOption(new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skill }, bot);
        }
    }
}
