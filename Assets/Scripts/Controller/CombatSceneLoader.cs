using UnityEngine;

public class CombatSceneLoader : MonoBehaviour
{
    [Header("Referências")]
    public AttackSequencer attackSequencer;         // Referência ao sequenciador
    public Transform player1SpawnPoint;             // Posição inicial (opcional)
    [SerializeField] private GameObject player2ObjectInScene;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;
        if (profile == null)
        {
            Debug.LogError("[CombatSceneLoader] Nenhum PlayerProfile selecionado.");
            return;
        }

        // Instanciar Player1
        GameObject player1 = Instantiate(profile.characterPrefab);
        player1.name = "Player1";
        player1.transform.position = profile.startPos;
        player1.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);


        var combat1 = player1.GetComponent<PlayerCombat>();
        if (combat1 == null)
        {
            Debug.LogError("[CombatSceneLoader] PlayerCombat não encontrado no Player1.");
            return;
        }

        var playerLoadout = player1.GetComponent<PlayerLoadout>();
        if (playerLoadout != null)
            playerLoadout.loadout = profile.weaponLoadout;

        var handler = player1.GetComponent<WeaponHandler>();
        if (handler != null)
            handler.loadout = playerLoadout;

        combat1.settings = profile.attackSettings;
        combat1.isPlayer1 = true;

        // Referência ao oponente chumbado na cena
        var combat2 = player2ObjectInScene.GetComponent<PlayerCombat>();
        var anim2 = player2ObjectInScene.GetComponent<AnimationController>();
        var anim1 = player1.GetComponent<AnimationController>();

        if (combat2 == null || anim1 == null || anim2 == null)
        {
            Debug.LogError("[CombatSceneLoader] Componentes do oponente faltando.");
            return;
        }

        // Link entre os dois
        combat1.defender = combat2;
        combat1.defenderAnimationController = anim2;

        combat2.defender = combat1;
        combat2.defenderAnimationController = anim1;

        // Registrar no sequenciador
        attackSequencer.player1 = combat1;
        attackSequencer.player2 = combat2;

        Debug.Log("[CombatSceneLoader] Combate pronto com ambos jogadores conectados.");
    }

}
