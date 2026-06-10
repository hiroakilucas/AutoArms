using UnityEngine;

public class CombatSceneLoader : MonoBehaviour
{
    [Header("References")]
    public AttackSequencer attackSequencer;
    [SerializeField] private GameObject player2Object;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    [Header("Player 2")]
    [Tooltip("Vida maxima do Player2 (Medieval Warrior Girl). Ajustar conforme o perfil do personagem.")]
    [SerializeField] private int player2MaxHealth = 15;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;
        if (profile == null)
        {
            Debug.LogError("[CombatSceneLoader] No PlayerProfile selected.");
            return;
        }

        var player2Combat = player2Object.GetComponent<PlayerCombat>();
        var player2Anim   = player2Object.GetComponent<AnimationController>();
        if (player2Combat == null || player2Anim == null)
        {
            Debug.LogError("[CombatSceneLoader] Player2 is missing PlayerCombat or AnimationController.");
            return;
        }

        GameObject player1Obj = Instantiate(profile.characterPrefab);
        player1Obj.name = "Player1";
        player1Obj.transform.position = profile.startPos;
        player1Obj.transform.localScale = Vector3.one * 0.3f;

        var player1Combat = player1Obj.GetComponent<PlayerCombat>();
        var player1Anim   = player1Obj.GetComponent<AnimationController>();
        if (player1Combat == null || player1Anim == null)
        {
            Debug.LogError("[CombatSceneLoader] Player1 prefab is missing PlayerCombat or AnimationController.");
            return;
        }

        var loadout = player1Obj.GetComponent<PlayerLoadout>();
        if (loadout != null) loadout.loadout = profile.weaponLoadout;

        var handler = player1Obj.GetComponent<WeaponHandler>();
        if (handler != null) handler.loadout = loadout;

        player1Combat.settings  = profile.attackSettings;
        player1Combat.isPlayer1 = true;
        player1Combat.defender  = player2Combat;
        player1Combat.defenderAnimationController = player2Anim;

        player2Combat.defender  = player1Combat;
        player2Combat.defenderAnimationController = player1Anim;

        // Inicializa sistema de vida e HUD
        var health1 = player1Obj.AddComponent<HealthSystem>();
        health1.Initialize(profile.maxHealth);

        var health2 = player2Object.GetComponent<HealthSystem>() ?? player2Object.AddComponent<HealthSystem>();
        health2.Initialize(player2MaxHealth);

        gameObject.AddComponent<CombatHUD>().Initialize(health1, health2);

        attackSequencer.player1 = player1Combat;
    }
}
