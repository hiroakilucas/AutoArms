using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AttackSequencer : MonoBehaviour
{
    [Header("Combatants")]
    public PlayerCombat player1;
    public PlayerCombat player2;

    [Header("Progresso")]
    public PlayerProfile player1Profile;
    // Setado por CombatSceneLoader junto de player1Profile — só usado aqui pra alimentar o
    // histórico de batalhas/vitórias por oponente (PlayerPrefs), ver OnCombatEnd.
    public PlayerProfile player2Profile;

    // Setados por CombatSceneLoader logo após CombatSimulator.Simulate() rodar (caminho do
    // simulador) — usados só em OnCombatEnd pra gravar o replay (ver ReplayRecorder). Ficam vazios
    // no caminho legado (useSimulator=false), onde OnCombatEnd simplesmente não grava replay.
    [HideInInspector] public int lastCombatSeed;
    [HideInInspector] public int lastCombatRoundCount;
    [HideInInspector] public List<CombatEvent> lastCombatEvents;

    // true quando esta luta é a REPRODUÇÃO de um replay salvo (ver ReplayPlaybackState/
    // CombatSceneLoader) — OnCombatEnd pula XP/save/histórico/gravação de outro replay nesse modo
    // e mostra um painel mínimo (ReplayEndPanel) em vez do CombatResultPanel normal.
    [HideInInspector] public bool isReplayPlayback;

    [Header("Level-Up Options")]
    public SkillDatabase skillDatabase;
    public WeaponData[]  allWeapons;
    // T1 dos 3 pets (2026-07-16, mesmo padrão de skillDatabase/allWeapons acima) — wireado
    // manualmente no Inspector com os assets gerados por Tools > AutoArms > Generate Pet Tiers.
    public PetData[]     petPool;
    // Wireado no Inspector (2026-07-21, mesmo asset usado em ShopController/ArsenalController/etc)
    // — repassado pro CombatResultPanel só pra construir o painel de detalhamento (CharacterPanel)
    // da tela de escolha de level-up. `null` é seguro (CombatResultPanel só constrói o painel se
    // `theme != null`) — nenhuma outra lógica de combate depende deste campo.
    public UITheme theme;

    [Header("Turn Settings")]
    public float interTurnDelay = 0.2f;

    int _p1SpeedDebt;
    int _p2SpeedDebt;

    private void Start()
    {
        StartCoroutine(StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        yield return new WaitUntil(() => player1 != null && player2 != null);
        // Initiative decide quem age primeiro; em empate (default 0 pra todo personagem sem
        // skill que a altere), quem tem mais speed age primeiro. Empate total continua player1.
        bool p2First = player1.initiative != player2.initiative
            ? player2.initiative > player1.initiative
            : player2.speed > player1.speed;
        StartCoroutine(CombatLoop(p2First));
    }

    private IEnumerator CombatLoop(bool player2GoesFirst = false)
    {
        PlayerCombat first  = player2GoesFirst ? player2 : player1;
        PlayerCombat second = player2GoesFirst ? player1 : player2;

        while (true)
        {
            // Accumulate speed debt for both players each round.
            _p1SpeedDebt += player1.speed;
            _p2SpeedDebt += player2.speed;

            // Count actions: while debt >= opponent's speed, consume one action's worth.
            int p1Act = 0, p2Act = 0;
            while (_p1SpeedDebt >= player2.speed) { p1Act++; _p1SpeedDebt -= player2.speed; }
            while (_p2SpeedDebt >= player1.speed) { p2Act++; _p2SpeedDebt -= player1.speed; }
            // Every player always acts at least once per round.
            p1Act = Mathf.Max(1, p1Act);
            p2Act = Mathf.Max(1, p2Act);

            int firstAct  = player2GoesFirst ? p2Act : p1Act;
            int secondAct = player2GoesFirst ? p1Act : p2Act;

            // First player executes all their actions, then second player.
            for (int i = 0; i < firstAct; i++)
            {
                if (i > 0)
                    DamagePopup.SpawnRapido(first.transform.position + Vector3.up * 2f);
                yield return StartCoroutine(first.AttackRoutine());
                if (second.IsDead) { OnCombatEnd(first); yield break; }
                yield return new WaitForSeconds(interTurnDelay);
            }

            for (int i = 0; i < secondAct; i++)
            {
                if (i > 0)
                    DamagePopup.SpawnRapido(second.transform.position + Vector3.up * 2f);
                yield return StartCoroutine(second.AttackRoutine());
                if (first.IsDead) { OnCombatEnd(second); yield break; }
                yield return new WaitForSeconds(interTurnDelay);
            }
        }
    }

    public void OnCombatEnd(PlayerCombat winner)
    {
        PlayerCombat.CleanupFallenWeapons();
        // Pets caídos NÃO são destruídos (preparação pra Tamer) — só limpa a lista estática
        // de rastreamento entre lutas, mesmo padrão de CleanupFallenWeapons.
        PetCombatController.CleanupDeadPets();

        // Reprodução de replay — nenhum efeito colateral de progresso (XP/save/histórico/gravar
        // outro replay): player1Profile aqui é um PlayerProfile RUNTIME reconstruído do snapshot
        // (ver ReplaySnapshotConverter), não o personagem real do jogador; tratá-lo como se fosse
        // salvaria stats CONGELADOS por cima do progresso de verdade.
        if (isReplayPlayback)
        {
            gameObject.AddComponent<ReplayEndPanel>().Show(winner.isPlayer1, player1Profile, player2Profile);
            return;
        }

        if (player1Profile == null) return;

        // Consumo de energia (2026-07-20, movido de MainMenuController.OnPlayButton) — só acontece
        // aqui, depois que a luta de fato termina em vitória ou derrota. Antes a energia era gasta
        // no clique do botão Jogar, então desistir em 05_SelectOpponent (ou fechar o jogo no meio
        // do combate) já cobrava a energia sem nenhuma luta concluída; bug real reportado pelo
        // usuário. Fire-and-forget (mesmo padrão de FirestoreService/ReplayRecorder logo abaixo) —
        // sem asset/conta, simplesmente não desconta (mesma tolerância que já existia no botão).
        if (AuthService.IsSignedIn)
            _ = ConsumeEnergyAfterCombatAsync(AuthService.CurrentUser.UserId, player1Profile.OpponentId());

        // Não usar "winner == player1": o campo player1 nunca é atribuído no caminho do
        // simulador (CombatSceneLoader só seta player1Profile, pra StartWhenReady/CombatLoop
        // legado não disparar em paralelo com o CombatPlayer) — isso fazia player1Won ser
        // sempre falso, mostrando DERROTA e dando XP de derrota mesmo quando P1 vencia.
        bool player1Won = winner.isPlayer1;
        // Bônus de XP dos Passes mensais da Loja (2026-07-21, pedido do usuário) — só na VITÓRIA,
        // nunca na derrota. WinXpBonus() soma 0 (sem passe)/1 (só Básico)/2 (só Pro)/3 (os dois),
        // fechando nos totais pedidos: 2, 3, 4, 5 — ver PlayerPassState.WinXpBonus.
        int  xpGained   = player1Won ? 2 + PlayerPassState.WinXpBonus() : 1;

        player1Profile.battlesRemaining = Mathf.Max(0, player1Profile.battlesRemaining - 1);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(player1Profile);
#endif

        int xpBefore    = player1Profile.xpCurrent;
        int levelBefore = player1Profile.level;
        var result      = XpSystem.AddXP(player1Profile, xpGained);

        // Save real (2026-07-15, corrigido — bug real reportado pelo usuário: Firestore ficava
        // "um passo atrás" do valor final de personagem; 2026-07-25, 2ª rodada — bug real
        // corrigido de novo: guardar esse save até o jogador escolher o bônus de level-up
        // significava que fechar o app com a tela de escolha aberta perdia XP/level/
        // battlesRemaining/bônus da luta inteira, sem chance de retomar — ver investigação em
        // CHANGELOG.md/ARQUITETURA.md). Salva SEMPRE agora, incondicional — a parte "base"
        // (battlesRemaining/xpCurrent/level/+2 HP automático) nunca dependeu da escolha
        // pendente, só ficava represada por cautela. `hasPendingLevelUpChoice` marca se há uma
        // escolha de skill/arma/pet/status ainda por resolver (CombatResultPanel preenche
        // pendingLevelUpBoxes logo a seguir, quando sorteia as caixas) — MainMenuController
        // detecta esse campo ao carregar o menu e reabre a MESMA tela de escolha se o app tiver
        // fechado antes do jogador decidir (ver CombatResultPanel.ResumePendingLevelUpChoiceIfAny).
        // Limpo só em CombatResultPanel.ApplyBonus, quando a escolha de fato acontece.
        player1Profile.hasPendingLevelUpChoice = result.didLevelUp;
        LocalSaveService.Save(player1Profile);

        // Histórico de batalhas/vitórias por oponente (05_SelectOpponent) — guardado em
        // PlayerPrefs por opponentId (continua sendo a fonte de leitura do card, sem mudança).
        // Repetir o mesmo PlayerProfile em vários slots do pool (ex: 6x Medieval Warrior Girl)
        // soma no mesmo contador de propósito, ver PlayerProfile.OpponentId.
        if (player2Profile != null)
        {
            string opponentId = player2Profile.OpponentId();
            int battles = PlayerPrefs.GetInt("battles_" + opponentId, 0) + 1;
            int wins    = PlayerPrefs.GetInt("wins_" + opponentId, 0) + (player1Won ? 1 : 0);
            PlayerPrefs.SetInt("battles_" + opponentId, battles);
            PlayerPrefs.SetInt("wins_" + opponentId, wins);
            PlayerPrefs.Save();

            // Espelho no Firestore (Fatia 6, 2026-07-15) — fire-and-forget, mesmo padrão de
            // LocalSaveService.Save. Não substitui o PlayerPrefs acima (continua sendo o que o
            // card em SelectOpponentController lê), só garante que o dado também exista na nuvem.
            if (AuthService.IsSignedIn)
                _ = FirestoreService.SaveMatchHistoryAsync(AuthService.CurrentUser.UserId, opponentId, battles, wins);
        }

        // Replay (log de eventos completo — ver ARQUITETURA.md/CHANGELOG.md, 2026-07-18) — mesmo
        // guard/padrão fire-and-forget acima; só grava quando veio do caminho do simulador
        // (lastCombatEvents é preenchido só por CombatSceneLoader, useSimulator=true).
        if (AuthService.IsSignedIn)
            ReplayRecorder.Save(AuthService.CurrentUser.UserId, player1Profile, player2Profile,
                player1Won, lastCombatSeed, lastCombatRoundCount, lastCombatEvents);

        gameObject.AddComponent<CombatResultPanel>()
            .Show(player1Won, xpGained, xpBefore, levelBefore, player1Profile, result.didLevelUp,
                  skillDatabase, allWeapons, petPool, theme);
    }

    // Mesma leitura/consumo que MainMenuController.OnPlayButton fazia antes (GetOrRegenAsync
    // pra aplicar regeneração pendente + ConsumeOneAsync) — só que agora rodando no momento em
    // que a luta termina, não no clique do botão. Sem o asset (Resources/EnergySettings.asset
    // ausente) não desconta nada, mesma tolerância que já existia lá.
    private async Task ConsumeEnergyAfterCombatAsync(string uid, string characterId)
    {
        var settings = Resources.Load<EnergySettings>("EnergySettings");
        if (settings == null) return;

        var (current, _) = await EnergyService.GetOrRegenAsync(uid, characterId, settings);
        if (current > 0)
            await EnergyService.ConsumeOneAsync(uid, characterId, current);
    }
}
