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
}
