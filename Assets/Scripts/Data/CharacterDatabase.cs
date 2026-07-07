using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game/Character Database", order = 101)]
public class CharacterDatabase : ScriptableObject
{
    [Tooltip("Lista de personagens desbloqueados que o jogador pode selecionar")]
    public List<PlayerProfile> unlockedCharacters = new List<PlayerProfile>();

    // Pool de oponentes pra 05_SelectOpponent — separado de unlockedCharacters porque inclui
    // personagens que não são jogáveis pelo jogador (ex: Medieval Warrior Girl). Hoje só tem a
    // Girl repetida 6x como placeholder (só existem 3 PlayerProfile no projeto até agora) — o
    // grid mostra quantos existirem aqui, até um teto de 6 (ver SelectOpponentController).
    [Tooltip("Pool de oponentes exibidos em 05_SelectOpponent (até 6)")]
    public List<PlayerProfile> opponentCharacters = new List<PlayerProfile>();
}
