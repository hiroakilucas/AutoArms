using UnityEngine;
using System;
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

    // 12 PlayerProfile já existentes (reaproveitando visual/prefab/ícone/splash art) usados como
    // "esqueleto" pros bots de matchmaking — SelectOpponentController.GenerateBotOpponents gera um
    // PlayerProfile runtime por template via BotProfileGenerator (stats/skills/armas escalados pro
    // level do jogador), preenchendo o grid quando a busca online (OpponentSearchService) não
    // retorna gente real suficiente. Fallback intermediário: online > bots > opponentCharacters
    // (placeholder de última instância). Populado por Tools > AutoArms > Setup Bot Templates.
    [Tooltip("12 PlayerProfile usados como identidade visual dos bots de matchmaking (stats reais gerados em runtime, ver BotProfileGenerator)")]
    public List<PlayerProfile> botTemplates = new List<PlayerProfile>();

    // Personagens elegíveis pra troca rápida (setas/arraste do personagem central em
    // 01_MainMenu, ver MainMenuCharacterPreview) — mesmo filtro isUnlockedForSelection &&
    // isPlayable e mesma ordem (favoritado primeiro, depois alfabético) do grid de
    // 02_SelectCharacter, pra não divergir entre as duas telas.
    public List<PlayerProfile> GetPlayableCharactersOrdered()
    {
        var list = new List<PlayerProfile>();
        foreach (var p in unlockedCharacters)
            if (p != null && p.isUnlockedForSelection && p.isPlayable) list.Add(p);
        list.Sort(ComparePlayerProfiles);
        return list;
    }

    // Favoritado (PlayerProfile.isFavorite) primeiro; depois por raridade crescente (Normal/
    // "comum" → Uncommon → Rare → Legendary → Immortal, 2026-07-14, pedido do usuário — a ordem
    // dos valores do enum CharacterRarity já é essa, então comparar os ints já dá o resultado
    // certo); dentro do mesmo favorito+raridade, ordem alfabética por profileName. Centralizado
    // aqui (em vez de duplicado em CharacterSelectController e MainMenuCharacterPreview) pra
    // grid e troca rápida sempre concordarem na mesma ordem.
    public static int ComparePlayerProfiles(PlayerProfile a, PlayerProfile b)
    {
        int favCompare = (b.isFavorite ? 1 : 0) - (a.isFavorite ? 1 : 0);
        if (favCompare != 0) return favCompare;
        int rarityCompare = ((int)a.rarity).CompareTo((int)b.rarity);
        if (rarityCompare != 0) return rarityCompare;
        return string.Compare(a.profileName, b.profileName, StringComparison.OrdinalIgnoreCase);
    }
}
