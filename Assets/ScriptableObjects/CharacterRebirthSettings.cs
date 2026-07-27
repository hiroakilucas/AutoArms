using UnityEngine;

// Balanceamento do "Renascimento" (Reset Nível 10+, 2026-07-26) — separado em ScriptableObject
// pelo mesmo motivo de EnergySettings.cs/CharacterResetSettings.cs: ajustável no Inspector sem
// precisar mexer em código. Asset PRÓPRIO, separado de CharacterResetSettings — mesmo princípio
// já usado no projeto pra reroll ("dois sistemas econômicos deliberadamente separados", ver
// rerollUnlock.ts).
//
// ECONOMIA INVERTIDA (correção de escopo, 2026-07-26, mesmo dia da 1ª versão) — Renascimento é
// 100% GRATUITO (sem custo em Coins nem Diamantes) e CREDITA nível×coinRewardPerLevel moedas ao
// concluir (era DÉBITO na versão original, campo se chamava `coinCostPerLevel`). Igual ao
// "Resetar Personagem" antigo nesse sentido (também credita), mas continua um sistema separado:
// só libera em level >= 10 e concede skills/armas/pets em vez de limpar o loadout. Mesmo valor
// numérico hoje (10) por coincidência com CharacterResetSettings.coinsPerLevel, não por
// acoplamento — ajustar um não deve afetar o outro. Espelhado no servidor em
// functions/src/rebirthCharacter.ts (REBIRTH_COIN_REWARD_PER_LEVEL) — mesmo padrão de
// REROLL_COST_DIAMONDS/generateLevel1Stats(), sempre mantidos em sincronia manualmente.
[CreateAssetMenu(fileName = "CharacterRebirthSettings", menuName = "Game/Character Rebirth Settings", order = 106)]
public class CharacterRebirthSettings : ScriptableObject
{
    [Tooltip("Moeda concedida ao renascer = nível do personagem ANTES do reset × este valor.")]
    public int coinRewardPerLevel = 10;
}
