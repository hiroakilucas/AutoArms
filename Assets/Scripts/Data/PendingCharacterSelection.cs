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

    // true só quando o pedido vem do onboarding (ChooseFirstCharacterController) — 2026-07-25,
    // bug real reportado pelo usuário: conta nova concede o 1º personagem, mas se o jogador
    // fecha o painel de detalhe em 02_SelectCharacter em vez de clicar "Selecionar",
    // SelectedProfileHolder nunca é atualizado (comportamento normal/intencional pro fluxo de
    // case opening, onde "só ver" o personagem sem trocar o ativo é válido) e fica preso no
    // default do asset (Medieval Warrior) — personagem que essa conta não possui de verdade,
    // causando "Missing or insufficient permissions" ao ler energia dele. Numa conta nova não
    // existe nenhuma seleção anterior válida pra preservar, então este flag faz
    // CharacterSelectController.ResolvePendingCharacterSelectionAsync confirmar automaticamente
    // o personagem concedido, independente de qual botão o jogador clicar depois. CaseOpeningPopup
    // nunca seta isto (fica false), preservando o comportamento de sempre pra ele.
    public static bool AutoConfirmSelection;
}
