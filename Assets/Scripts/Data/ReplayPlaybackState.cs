using System.Collections.Generic;

// Canal cross-scene só pro modo "assistir replay" — deliberadamente um campo estático puro, NÃO
// um ScriptableObject em Resources (o padrão de sempre pro resto do projeto, ver CLAUDE.md
// "ScriptableObject Assets"). Motivo: os dois PlayerProfile reconstruídos do snapshot (P1Profile/
// P2Profile) já usam o canal de sempre por baixo (SelectedProfileHolder/SelectedOpponentHolder
// continuam intocados — CombatSceneLoader lê P1Profile/P2Profile DAQUI em vez dos holders quando
// IsActive, então o personagem/oponente "de verdade" do jogador nunca é sobrescrito); só falta um
// jeito de dizer "não rode CombatSimulator, use ESTES eventos já prontos", o que não é um
// "personagem selecionado" e não faz sentido como asset editável no Inspector. Mesmo padrão de
// estado-em-memória-de-sessão que PlayerProfileConverter já usa (_pristineSnapshots/_ownerScope).
//
// Consumido (Clear()) assim que CombatSceneLoader.Initialize() lê os eventos — uma luta NORMAL
// seguinte nunca deve acidentalmente cair no modo replay.
public static class ReplayPlaybackState
{
    public static PlayerProfile P1Profile;
    public static PlayerProfile P2Profile;
    public static List<CombatEvent> Events;

    public static bool IsActive => P1Profile != null && P2Profile != null && Events != null && Events.Count > 0;

    public static void Set(PlayerProfile p1Profile, PlayerProfile p2Profile, List<CombatEvent> events)
    {
        P1Profile = p1Profile;
        P2Profile = p2Profile;
        Events = events;
    }

    public static void Clear()
    {
        P1Profile = null;
        P2Profile = null;
        Events = null;
    }
}
