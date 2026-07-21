// Canal cross-scene só pros Passes mensais comprados na Loja (Fase 1 — placeholder, ver
// MONETIZACAO.md seção 3 "Passes mensais") — mesmo espírito de PlayerEconomyState/
// PlayerUnlocksState: campos estáticos puros (não ScriptableObject), populados em memória por
// ShopController.OnBuyClicked (aba Passes).
//
// Básico e Pro são INDEPENDENTES (2026-07-21, revisado a pedido do usuário — não existe mais um
// "tier único"/upgrade entre os dois): cada um tem seu próprio contador de dias restantes, e os
// dois podem estar ativos ao mesmo tempo. WinXpBonus() é a única lógica de recompensa já
// implementada de verdade (lida por AttackSequencer.OnCombatEnd a cada vitória) — a coleta diária
// de diamante/moeda ainda não está implementada, ver ShopController.ShowPassInfoPopup.
//
// Persistência real (2026-07-21) — ShopStateService.cs grava em users/{uid}
// (passBasicoDaysRemaining/passProDaysRemaining). Carregado em LoginController.LoadEconomyRoutine
// e MainMenuController.RefreshEconomyOnMenuLoad (mesmo padrão de PlayerEconomyState/
// PlayerUnlocksState) — precisa rodar ANTES de qualquer combate, senão WinXpBonus() sempre
// retorna 0 mesmo com passe ativo no Firestore (bug real corrigido nesta data: só
// ShopController.Start() carregava, então lutar sem visitar a Loja nesta sessão não dava o bônus
// de XP mesmo com o passe comprado). TODO SEGURANÇA (permanece): gravação ainda client-side, sem
// Cloud Function — mesma classe de problema documentada em ARQUITETURA.md "Moeda premium".
public static class PlayerPassState
{
    public static int BasicoDaysRemaining;
    public static int ProDaysRemaining;

    // Bônus de XP por vitória (pedido do usuário, 2026-07-21) — somado ao XP base de vitória (2,
    // ver AttackSequencer.OnCombatEnd) pra fechar exatamente nos totais pedidos: só Básico → 3
    // (2+1), só Pro → 4 (2+2), os dois → 5 (2+3). Só conta enquanto o(s) passe(s) estiver(em)
    // dentro do período de 30 dias (`*DaysRemaining > 0`) — nunca decrementado aqui, ver
    // ShopController.ApplyPassPurchase pra quem escreve os contadores.
    public static int WinXpBonus()
    {
        bool basico = BasicoDaysRemaining > 0;
        bool pro = ProDaysRemaining > 0;
        if (basico && pro) return 3;
        if (pro) return 2;
        if (basico) return 1;
        return 0;
    }
}
