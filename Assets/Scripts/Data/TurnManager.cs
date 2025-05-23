using UnityEngine;
using System.Collections;

public class TurnManager : MonoBehaviour
{
    [Tooltip("Referência ao Player 1")]
    public PlayerCombat player1;

    [Tooltip("Referência ao Player 2")]
    public PlayerCombat player2;

    [Tooltip("Delay entre turnos")]
    public float interTurnDelay = 0.2f;

    private void Start()
    {
        StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        while (true)
        {
            // Player 1 ataca
            yield return StartCoroutine(player1.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);

            // Player 2 ataca
            yield return StartCoroutine(player2.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);
        }
    }
}
