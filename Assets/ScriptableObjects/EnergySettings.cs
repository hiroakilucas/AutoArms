using UnityEngine;

// Balanceamento do sistema de energia (2026-07-19) — separado em ScriptableObject pra poder ser
// ajustado pelo usuário no Inspector sem precisar de código novo. Ver EnergyService.cs pra lógica
// de regeneração/consumo.
[CreateAssetMenu(fileName = "EnergySettings", menuName = "Game/Energy Settings", order = 104)]
public class EnergySettings : ScriptableObject
{
    [Tooltip("Energia máxima por personagem (10 batalhas diárias).")]
    public int maxEnergy = 10;

    [Tooltip("Horas pra regenerar +1 energia, até o teto de maxEnergy.")]
    public float regenIntervalHours = 2f;

    // Substituiu diamondCostToRefill (custo fixo único) — 2026-07-21, pedido do usuário: preço
    // agora é PROGRESSIVO por dia, POR PERSONAGEM (não por conta — ver EnergyService.
    // GetRefillCostAsync/PayToRefillAsync). Contador de "quantas vezes pagou hoje" reseta à meia-
    // noite em hora do SERVIDOR (nunca o relógio do device, mesmo padrão de
    // EnergyService.ReadServerNowAsync).
    [Tooltip("PLACEHOLDER — valor inicial de balanceamento, ajustar livremente. Diamantes cobrados na 1ª vez que o jogador paga pra continuar jogando um personagem específico no mesmo dia (hora do servidor).")]
    public int refillCostTier1 = 10;

    [Tooltip("Diamantes cobrados na 2ª vez no mesmo dia (mesmo personagem).")]
    public int refillCostTier2 = 20;

    [Tooltip("Diamantes cobrados na 3ª vez em diante no mesmo dia (mesmo personagem) — travado, não dobra mais.")]
    public int refillCostTier3Plus = 40;
}
