// Canal estático cross-scene (mesmo padrão de PlayerEconomyState/PlayerUnlocksState/
// PlayerPassState) — grava as compras da aba Progressão da Loja (Slot de Skill 1/2/3) assim que
// acontecem, pra sobreviver à troca de cena (a Loja em si é Fase 1/placeholder, estado local do
// ShopController, perdido ao sair de 06_Loja — este canal é o que fica). Lido por
// CombatResultPanel pra decidir quantas caixas de bônus o level-up oferece.
//
// Persistência real (2026-07-21) — ShopStateService.cs grava em users/{uid}
// (progressionSlot1/2/3Unlocked). Carregado em LoginController.LoadEconomyRoutine e
// MainMenuController.RefreshEconomyOnMenuLoad (mesmo padrão de PlayerEconomyState/
// PlayerUnlocksState/PlayerPassState) — precisa rodar ANTES de qualquer level-up, senão
// LevelUpBoxCount() sempre volta pro mínimo (2) mesmo com slots comprados no Firestore (bug real
// corrigido nesta data: só ShopController.Start() carregava, então subir de nível sem visitar a
// Loja nesta sessão não oferecia as caixas extras já compradas). TODO SEGURANÇA (permanece):
// gravação ainda client-side, sem Cloud Function — mesma classe de problema documentada em
// ARQUITETURA.md "Moeda premium".
public static class PlayerProgressionState
{
    public static bool Slot1Unlocked;
    public static bool Slot2Unlocked;
    public static bool Slot3Unlocked;

    // 2 caixas base (sempre) + 1 por slot comprado (até 5) — Caixa 1 é sempre status base, ver
    // CombatResultPanel.ShowLevelUpChoice.
    public static int LevelUpBoxCount()
    {
        int extra = (Slot1Unlocked ? 1 : 0) + (Slot2Unlocked ? 1 : 0) + (Slot3Unlocked ? 1 : 0);
        return 2 + extra;
    }
}
