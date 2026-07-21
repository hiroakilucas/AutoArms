using UnityEngine;

// Balanceamento do "Resetar Personagem" (2026-07-21) — separado em ScriptableObject pelo mesmo
// motivo de EnergySettings.cs: ajustável no Inspector sem precisar mexer em código. Ver
// CharacterPanel.cs (botão "Resetar Personagem", painel de detalhamento) pra lógica de uso —
// mecanismo PARALELO ao "Reset de Build" (ROADMAP_FUTURO.md Fase 4, ainda não implementado, custa
// diamante e mantém o nível) — este reseta pro nível 1 igual à ferramenta de Editor "Reset All
// Profiles to Level 1", mas GERA moeda em vez de custar diamante.
[CreateAssetMenu(fileName = "CharacterResetSettings", menuName = "Game/Character Reset Settings", order = 105)]
public class CharacterResetSettings : ScriptableObject
{
    [Tooltip("Moeda gerada ao resetar = nível do personagem ANTES do reset × este valor.")]
    public int coinsPerLevel = 10;
}
