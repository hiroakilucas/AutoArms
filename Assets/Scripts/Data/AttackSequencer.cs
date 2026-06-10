using UnityEngine;
using System.Collections;

public class AttackSequencer : MonoBehaviour
{
    [Header("Combatants")]
    public PlayerCombat player1;
    public PlayerCombat player2;

    [Header("Turn Settings")]
    public float interTurnDelay = 0.2f;

    private void Start()
    {
        StartCoroutine(StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        yield return new WaitUntil(() => player1 != null && player2 != null);
        StartCoroutine(CombatLoop());
    }

    private IEnumerator CombatLoop()
    {
        while (true)
        {
            yield return StartCoroutine(player1.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);
            yield return StartCoroutine(player2.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);
        }
    }
}
