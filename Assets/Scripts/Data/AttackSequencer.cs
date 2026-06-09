using UnityEngine;
using System.Collections;

public class AttackSequencer : MonoBehaviour
{
    [Header("Combatentes")]
    [Tooltip("PlayerCombat do Player 1")]
    public PlayerCombat player1;
    [Tooltip("PlayerCombat do Player 2")]
    public PlayerCombat player2;

    [Header("Configuração de Turnos")]
    [Tooltip("Delay entre os turnos")]
    public float interTurnDelay = 0.2f;

    private void Start()
    {
        StartCoroutine(WaitForPlayersAndStart());
    }

    private IEnumerator WaitForPlayersAndStart()
    {
        // Espera até que ambos players estejam instanciados
        while (player1 == null || player2 == null)
        {
            Debug.Log("[TurnManager] Aguardando instância dos dois jogadores...");
            yield return null;
        }

        StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        while (true)
        {
            // Turno do Player 1
            yield return StartCoroutine(player1.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);

            // Turno do Player 2
            yield return StartCoroutine(player2.AttackRoutine());
            yield return new WaitForSeconds(interTurnDelay);
        }
    }
}
