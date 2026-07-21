using System.Collections.Generic;
using UnityEngine;

// Reconstrói um PlayerProfile RUNTIME (não um asset do projeto) a partir de um
// ReplayPlayerSnapshotDTO — mesmo espírito de PlayerProfileConverter.FromOpponentIndexMap (usado
// pra reconstruir um adversário achado em opponents_index), mas partindo do snapshot congelado de
// um replay em vez de um documento "vivo" de busca de oponente. Usado só pra alimentar
// CombatSceneLoader/CombatPlayer durante a reprodução — nunca persistido de volta (ver
// AttackSequencer.isReplayPlayback, que pula qualquer save/XP/histórico nesse modo).
public static class ReplaySnapshotConverter
{
    public static PlayerProfile ToRuntimeProfile(ReplayPlayerSnapshotDTO snapshot, CharacterDatabase templateCatalog)
    {
        if (snapshot == null || templateCatalog?.unlockedCharacters == null) return null;

        // Casado por profileName (não pelo nome do asset Unity, que o snapshot não guarda) —
        // profileName já é a chave que todo o resto da UI usa pra exibir/identificar o personagem
        // (CharacterPanel, SelectOpponentController etc.), então é único o suficiente na prática.
        PlayerProfile template = null;
        foreach (var t in templateCatalog.unlockedCharacters)
        {
            if (t != null && t.profileName == snapshot.profileName) { template = t; break; }
        }
        // Personagem removido/renomeado desde que o replay foi gravado — sem molde, não dá pra
        // resolver characterPrefab/attackSettings/scale nenhum; falha graciosamente (replay
        // simplesmente não pode ser reproduzido) em vez de instanciar um personagem quebrado.
        if (template == null) return null;

        var runtime = ScriptableObject.CreateInstance<PlayerProfile>();
        runtime.characterId = template.characterId;
        runtime.profileName = template.profileName;
        runtime.characterPrefab = template.characterPrefab;
        runtime.previewIcon = template.previewIcon;
        runtime.splashArt = template.splashArt;
        runtime.attackSettings = template.attackSettings;
        runtime.scale = template.scale;
        runtime.startPos = template.startPos;
        // Nunca deve aparecer no grid/troca rápida do jogador — só existe pra virar
        // ReplayPlaybackState.P1Profile/P2Profile.
        runtime.isUnlockedForSelection = false;
        runtime.isPlayable = false;

        runtime.maxHealth = snapshot.maxHealth;
        runtime.str = snapshot.str;
        runtime.agility = snapshot.agility;
        runtime.speed = snapshot.speed;
        runtime.armor = snapshot.armor;
        runtime.evasion = snapshot.evasion;
        runtime.accuracy = snapshot.accuracy;
        runtime.initiative = snapshot.initiative;
        runtime.reversal = snapshot.reversal;
        runtime.counter = snapshot.counter;
        runtime.blockBonus = snapshot.blockBonus;
        runtime.reversalAfterBlock = snapshot.reversalAfterBlock;
        runtime.criticalChance = snapshot.criticalChance;
        runtime.hitSpeed = snapshot.hitSpeed;

        var weaponDb = Resources.Load<WeaponDatabase>("WeaponDatabase");
        if (weaponDb != null && snapshot.weapons != null)
        {
            var weapons = new List<WeaponData>();
            foreach (var w in snapshot.weapons)
            {
                var data = weaponDb.FindByFamilyNameAndTier(w.name, w.tier);
                if (data != null) weapons.Add(data);
            }
            runtime.weapons = weapons;
        }

        var skillDb = Resources.Load<SkillDatabase>("SkillDatabase");
        if (skillDb != null && snapshot.skills != null)
        {
            var skills = new List<SkillData>();
            foreach (var s in snapshot.skills)
            {
                var data = skillDb.FindByFamilyNameAndTier(s.name, s.tier);
                if (data != null) skills.Add(data);
            }
            runtime.skills = skills;
        }

        var petDb = Resources.Load<PetDatabase>("PetDatabase");
        if (petDb != null && snapshot.pets != null)
        {
            var pets = new List<PetData>();
            foreach (var p in snapshot.pets)
            {
                if (!System.Enum.TryParse(p.type, out PetType pt)) continue;
                var data = petDb.FindByTypeAndTier(pt, p.tier);
                if (data != null) pets.Add(data);
            }
            runtime.pets = pets;
        }

        return runtime;
    }
}
