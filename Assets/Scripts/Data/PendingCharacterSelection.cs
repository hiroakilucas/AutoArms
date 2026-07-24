// Canal estático cross-scene mínimo (mesmo padrão de ReplayPlaybackState) — 2026-07-24, sistema
// de compra de personagens/case opening. Único propósito: CaseOpeningPopup grava aqui o
// characterId do personagem recém-concedido antes de navegar pra 02_SelectCharacter;
// CharacterSelectController lê e LIMPA este campo assim que resolve a seleção (consumido uma
// única vez, nunca reaplicado numa visita futura à cena).
//
// Deliberadamente separado de SelectedProfileHolder — este último representa o personagem
// CONFIRMADO pra combate (só muda quando o jogador clica "Selecionar"); isto aqui é só um pedido
// de "destaque ao abrir a tela", um conceito diferente.
public static class PendingCharacterSelection
{
    public static string PendingCharacterId;
}
