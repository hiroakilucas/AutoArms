using UnityEngine;

// Balanceamento do "Renascimento" (Reset Nível 10+, 2026-07-26) — separado em ScriptableObject
// pelo mesmo motivo de EnergySettings.cs: ajustável no Inspector sem precisar mexer em código.
//
// ECONOMIA INVERTIDA (correção de escopo, 2026-07-26, mesmo dia da 1ª versão) — Renascimento é
// 100% GRATUITO (sem custo em Coins nem Diamantes) e CREDITA nível×coinRewardPerLevel moedas ao
// concluir (era DÉBITO na versão original, campo se chamava `coinCostPerLevel`). Substituiu por
// completo o antigo botão "Resetar Personagem" (removido 2026-07-27) — só libera em level >= 10
// e concede skills/armas/pets em vez de limpar o loadout. Espelhado no servidor em
// functions/src/rebirthCharacter.ts (REBIRTH_COIN_REWARD_PER_LEVEL) — mesmo padrão de
// REROLL_COST_DIAMONDS/generateLevel1Stats(), sempre mantidos em sincronia manualmente.
[CreateAssetMenu(fileName = "CharacterRebirthSettings", menuName = "Game/Character Rebirth Settings", order = 106)]
public class CharacterRebirthSettings : ScriptableObject
{
    [Tooltip("Moeda concedida ao renascer = nível do personagem ANTES do reset × este valor.")]
    public int coinRewardPerLevel = 10;
}
