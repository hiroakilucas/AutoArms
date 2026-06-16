using UnityEngine;
using System.Collections;

public class AttackSequencer : MonoBehaviour
{
    [Header("Combatants")]
    public PlayerCombat player1;
    public PlayerCombat player2;

    [Header("Progresso")]
    public PlayerProfile player1Profile;

    [Header("Level-Up Options")]
    public SkillDatabase skillDatabase;
    public WeaponData[]  allWeapons;

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
        bool p2First = player2.initiative > player1.initiative;
        Debug.Log($"[Initiative] Player1: {player1.initiative} vs Player2: {player2.initiative} → {(p2First ? "Player2" : "Player1")} ataca primeiro");
        StartCoroutine(CombatLoop(p2First));
    }

    private IEnumerator CombatLoop(bool player2GoesFirst = false)
    {
        PlayerCombat first  = player2GoesFirst ? player2 : player1;
        PlayerCombat second = player2GoesFirst ? player1 : player2;
        int round = 0;

        while (true)
        {
            round++;

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
            Debug.Log($"[Speed] Round {round}: P1 debt={_p1SpeedDebt} age={p1Act} | P2 debt={_p2SpeedDebt} age={p2Act}");

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

    private void OnCombatEnd(PlayerCombat winner)
    {
        Debug.Log($"[AttackSequencer] {winner.name} venceu o combate!");
        PlayerCombat.CleanupFallenWeapons();

        if (player1Profile == null) return;

        bool player1Won = winner == player1;
        int  xpGained   = player1Won ? 2 : 1;

        player1Profile.battlesRemaining = Mathf.Max(0, player1Profile.battlesRemaining - 1);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(player1Profile);
#endif

        int xpBefore    = player1Profile.xpCurrent;
        int levelBefore = player1Profile.level;
        var result      = XpSystem.AddXP(player1Profile, xpGained);

        gameObject.AddComponent<CombatResultPanel>()
            .Show(player1Won, xpGained, xpBefore, levelBefore, player1Profile, result.didLevelUp,
                  skillDatabase, allWeapons);
    }
}
