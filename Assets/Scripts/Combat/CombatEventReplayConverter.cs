using System;
using System.Collections.Generic;
using UnityEngine;

// CombatEvent (runtime, gerado por CombatSimulator/consumido por CombatPlayer) <-> ReplayEventDTO
// (formato salvo no Firestore). Ver ReplayEventDTO.cs para o motivo de existir uma camada própria
// em vez de serializar CombatEvent direto.
public static class CombatEventReplayConverter
{
    public static List<ReplayEventDTO> ToDTOList(List<CombatEvent> events)
    {
        var list = new List<ReplayEventDTO>(events.Count);
        foreach (var e in events) list.Add(ToDTO(e));
        return list;
    }

    public static ReplayEventDTO ToDTO(CombatEvent e) => new ReplayEventDTO
    {
        type = e.type.ToString(),
        playerIndex = e.playerIndex,
        targetIndex = e.targetIndex,
        damage = e.damage,
        isCrit = e.isCrit,
        isCombo = e.isCombo,
        isThrow = e.isThrow,
        isRetaliation = e.isRetaliation,
        isDodged = e.isDodged,
        isBlocked = e.isBlocked,
        isFierceBrute = e.isFierceBrute,
        newHp = e.newHp,
        maxHp = e.maxHp,
        extraActions = e.extraActions,
        healAmount = e.healAmount,
        pulseCount = e.pulseCount,
        newDefenderHp = e.newDefenderHp,
        newAttackerHp = e.newAttackerHp,
        weaponName = e.weaponName,
        ffWeapons = e.ffWeapons,
        ffDamages = e.ffDamages,
        ffHpAfter = e.ffHpAfter,
        bombTargets = e.bombTargets,
        bombTargetDamages = e.bombTargetDamages,
        bombTargetHp = e.bombTargetHp,
        netFreedTargets = e.netFreedTargets,
        bombPetIndexes = e.bombPetIndexes,
        bombPetHp = e.bombPetHp,
        petIndex = e.petIndex,
        targetPetIndex = e.targetPetIndex,
        targetIsPet = e.targetIsPet,
        newTargetHp = e.newTargetHp,
        newTargetMaxHp = e.newTargetMaxHp,
        shieldIntercept = e.shieldIntercept,
        petShieldAbsorb = e.petShieldAbsorb,
    };

    public static List<CombatEvent> FromDTOList(List<ReplayEventDTO> dtos)
    {
        var list = new List<CombatEvent>(dtos.Count);
        foreach (var d in dtos) list.Add(FromDTO(d));
        return list;
    }

    public static CombatEvent FromDTO(ReplayEventDTO d)
    {
        if (!Enum.TryParse(d.type, out CombatEventType type))
        {
            // Só deveria acontecer com um documento corrompido/de uma versão futura incompatível —
            // TurnEnd é o fallback mais inócuo (CombatPlayer só reposiciona quem já teria voltado
            // ao spawn de qualquer forma).
            Debug.LogError($"[CombatEventReplayConverter] Tipo de evento desconhecido no replay: '{d.type}'");
            type = CombatEventType.TurnEnd;
        }

        return new CombatEvent
        {
            type = type,
            playerIndex = d.playerIndex,
            targetIndex = d.targetIndex,
            damage = d.damage,
            isCrit = d.isCrit,
            isCombo = d.isCombo,
            isThrow = d.isThrow,
            isRetaliation = d.isRetaliation,
            isDodged = d.isDodged,
            isBlocked = d.isBlocked,
            isFierceBrute = d.isFierceBrute,
            newHp = d.newHp,
            maxHp = d.maxHp,
            extraActions = d.extraActions,
            healAmount = d.healAmount,
            pulseCount = d.pulseCount,
            newDefenderHp = d.newDefenderHp,
            newAttackerHp = d.newAttackerHp,
            weaponName = d.weaponName,
            ffWeapons = d.ffWeapons,
            ffDamages = d.ffDamages,
            ffHpAfter = d.ffHpAfter,
            bombTargets = d.bombTargets,
            bombTargetDamages = d.bombTargetDamages,
            bombTargetHp = d.bombTargetHp,
            netFreedTargets = d.netFreedTargets,
            bombPetIndexes = d.bombPetIndexes,
            bombPetHp = d.bombPetHp,
            petIndex = d.petIndex,
            targetPetIndex = d.targetPetIndex,
            targetIsPet = d.targetIsPet,
            newTargetHp = d.newTargetHp,
            newTargetMaxHp = d.newTargetMaxHp,
            shieldIntercept = d.shieldIntercept,
            petShieldAbsorb = d.petShieldAbsorb,
        };
    }
}
