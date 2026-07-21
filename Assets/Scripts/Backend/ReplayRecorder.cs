using System;
using System.Collections.Generic;

// Monta o ReplayDTO de uma luta recém-terminada e dispara a gravação em
// users/{uid}/characters/{characterId}/replays/{replayId} — chamado por
// AttackSequencer.OnCombatEnd, mesmo ponto/padrão fire-and-forget de SaveMatchHistoryAsync.
public static class ReplayRecorder
{
    public static void Save(string uid, PlayerProfile localProfile, PlayerProfile opponentProfile,
        bool localWon, int seed, int roundCount, List<CombatEvent> events)
    {
        if (localProfile == null || events == null || events.Count == 0) return;

        var dto = new ReplayDTO
        {
            createdAtTicks = DateTime.UtcNow.Ticks,
            opponentCharacterId = opponentProfile != null ? opponentProfile.OpponentId() : "",
            opponentName = opponentProfile != null ? opponentProfile.profileName : "",
            result = localWon ? "win" : "loss",
            seed = seed,
            eventCount = events.Count,
            roundCount = roundCount,
            p1Snapshot = CaptureSnapshot(localProfile),
            p2Snapshot = CaptureSnapshot(opponentProfile),
            events = CombatEventReplayConverter.ToDTOList(events),
        };

        _ = FirestoreService.SaveReplayAsync(uid, localProfile.OpponentId(), dto);
    }

    // Reaproveita PlayerProfileConverter.ToDTO (já resolve weapons/skills/pets pra
    // WeaponTierRef/SkillTierRef/PetTierRef, incluindo o strip do sufixo " T1/T2/T3") em vez de
    // duplicar aquele loop aqui — só remapeia pros campos combat-relevantes do snapshot (sem os
    // campos de progressão de CharacterDTO, que não fazem sentido "congelados" numa luta passada).
    private static ReplayPlayerSnapshotDTO CaptureSnapshot(PlayerProfile profile)
    {
        if (profile == null) return null;
        var characterDto = PlayerProfileConverter.ToDTO(profile);

        return new ReplayPlayerSnapshotDTO
        {
            profileName = characterDto.profileName,
            maxHealth = characterDto.maxHealth,
            str = characterDto.str,
            agility = characterDto.agility,
            speed = characterDto.speed,
            armor = characterDto.armor,
            evasion = characterDto.evasion,
            accuracy = characterDto.accuracy,
            initiative = characterDto.initiative,
            reversal = characterDto.reversal,
            counter = characterDto.counter,
            blockBonus = characterDto.blockBonus,
            reversalAfterBlock = characterDto.reversalAfterBlock,
            criticalChance = characterDto.criticalChance,
            hitSpeed = characterDto.hitSpeed,
            weapons = characterDto.weapons,
            skills = characterDto.skills,
            pets = characterDto.pets,
        };
    }
}
