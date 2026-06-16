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
        while (true)
        {
            yield return StartCoroutine(first.AttackRoutine());
            if (second.IsDead) { OnCombatEnd(first); yield break; }
            yield return new WaitForSeconds(interTurnDelay);

            yield return StartCoroutine(second.AttackRoutine());
            if (first.IsDead) { OnCombatEnd(second); yield break; }
            yield return new WaitForSeconds(interTurnDelay);
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
