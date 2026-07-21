// Canal cross-scene só pros desbloqueios permanentes comprados na Loja (ver MONETIZACAO.md seção
// 2 "Desbloqueios permanentes") — mesmo espírito de PlayerEconomyState: campo estático puro (não
// ScriptableObject), populado em memória por ShopController.OnBuyClicked quando o jogador compra
// Skip/Velocidade 1.5x/Bundle na aba Desbloqueios, e lido por CombatHUD pra decidir se os botões
// de Skip/1.5x aparecem habilitados durante o combate.
//
// Persistência real (2026-07-21) — ShopStateService.cs grava em users/{uid} (skipUnlocked/
// speed15xUnlocked). Carregado em 2 pontos (LoginController.LoadEconomyRoutine e MainMenuController
// .RefreshEconomyOnMenuLoad, mesmo padrão de PlayerEconomyState) — ambos precisam rodar ANTES de
// qualquer combate, senão este canal fica no default (tudo desligado) mesmo com o dado certo no
// Firestore (bug real corrigido nesta data: só ShopController.Start() carregava, então ir direto
// pra batalha sem visitar a Loja nesta sessão fazia os botões de Skip/1.5x ficarem desabilitados
// mesmo já comprados). TODO SEGURANÇA (permanece): gravação ainda client-side, sem Cloud Function
// — mesma classe de problema documentada em ARQUITETURA.md "Moeda premium" pra diamante.
public static class PlayerUnlocksState
{
    public static bool SkipUnlocked;
    public static bool Speed15xUnlocked;
}
