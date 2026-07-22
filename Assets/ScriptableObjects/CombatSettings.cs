using UnityEngine;

// Balanceamento do sistema de iniciativa ATB (turnos de Player1/Player2/pets) — separado em
// ScriptableObject pra poder ser ajustado no Inspector sem precisar de código novo, mesmo
// padrão de EnergySettings.cs. Ver CombatSimulator.RunInitiativeLoop.
[CreateAssetMenu(fileName = "CombatSettings", menuName = "Game/Combat Settings", order = 106)]
public class CombatSettings : ScriptableObject
{
    [Tooltip("Valor que o contador de iniciativa de cada combatente (Player1, Player2, cada pet) precisa atingir/ultrapassar para agir. Contador soma a própria speed a cada tick de simulação; ao cruzar, subtrai este valor (mantém overflow).")]
    public int initiativeThreshold = 100;
}
