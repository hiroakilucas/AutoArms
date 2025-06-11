using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game/Character Database", order = 101)]
public class CharacterDatabase : ScriptableObject
{
    [Tooltip("Lista de personagens desbloqueados que o jogador pode selecionar")]
    public List<PlayerProfile> unlockedCharacters = new List<PlayerProfile>();
}
