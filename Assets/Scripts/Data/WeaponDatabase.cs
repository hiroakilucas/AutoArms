using UnityEngine;
using System.Collections.Generic;

// Lista central de TODAS as armas do jogo (mesmo espírito de SkillDatabase, que já existia —
// armas nunca tiveram um database equivalente porque nada precisava enumerar "toda arma que
// existe" antes da tela de Arsenal; CombatSceneLoader/AttackSequencer sempre trabalharam só com
// o loadout de cada PlayerProfile). Só os T1 (raiz de cada família, previousTier == null)
// precisam estar aqui — T2/T3 são alcançados subindo weapon.nextTier a partir do T1, mesmo
// padrão de ResolveTierFamily em CharacterPanel.
[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Game/Weapon Database", order = 103)]
public class WeaponDatabase : ScriptableObject
{
    public List<WeaponData> weapons = new List<WeaponData>();

    // Acha a arma exata (T1/T2/T3) a partir do nome "de família" (sem sufixo de tier — ver
    // WeaponNameUtil.StripWeaponTierSuffix) e do tier desejado (2026-07-14) — usado pela camada
    // de save (LocalSaveService/PlayerProfileConverter) pra resolver de volta o WeaponData real a
    // partir do par nome+tier gravado no save (ScriptableObjects não podem ser serializados em
    // JSON/Firestore diretamente). `weapons` só lista os T1 (raiz de cada família) — sobe até o
    // tier pedido andando por `WeaponData.nextTier`, mesmo padrão de ResolveTierFamily em
    // CharacterPanel/ArsenalController.ResolveWeaponSlot.
    public WeaponData FindByFamilyNameAndTier(string familyName, int tier)
    {
        if (string.IsNullOrEmpty(familyName) || tier < 1) return null;
        foreach (var t1 in weapons)
        {
            if (t1 == null) continue;
            if (WeaponNameUtil.StripWeaponTierSuffix(t1.weaponName) != familyName) continue;

            var current = t1;
            for (int i = 1; i < tier && current != null; i++)
                current = current.nextTier;
            return current;
        }
        return null;
    }
}
