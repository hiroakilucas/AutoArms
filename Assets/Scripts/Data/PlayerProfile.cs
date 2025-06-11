using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerProfile", menuName = "Game/Player Profile", order = 100)]
public class PlayerProfile : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Nome do personagem para exibição")]
    public string profileName;

    [Header("Prefab e Imagem")]
    [Tooltip("Prefab do personagem (deve conter todos os componentes necessários para o combate)")]
    public GameObject characterPrefab;

    [Tooltip("Ícone utilizado na UI de seleção de personagens")]
    public Sprite previewIcon;

    [Header("Parâmetros de Combate")]
    [Tooltip("Configurações de ataque e animação (velocidade, idle, delay etc.)")]
    public AttackSettings attackSettings;

    [Tooltip("Armas atribuídas para esse personagem")]
    public WeaponLoadout weaponLoadout;

    [Header("Instanciação")]
    [Tooltip("Escala personalizada do personagem no momento da instância")]
    public Vector3 scale = Vector3.one;

    [Tooltip("Posição de início no combate (usada no PvP, exemplo: lado esquerdo)")]
    public Vector2 startPos = new Vector2(-6.3f, -2.407897f);

    [Header("Progresso")]
    [Tooltip("Nível atual do personagem")]
    public int level = 1;

    [Tooltip("Taxa de vitória (0 a 100%)")]
    [Range(0f, 100f)] public float winRate = 0f;

    [Tooltip("Experiência atual")]
    public int xpCurrent = 0;

    [Tooltip("Experiência necessária para o próximo nível")]
    public int xpRequired = 30;

    [Tooltip("Lutas restantes (máximo por ciclo)")]
    public int battlesRemaining = 6;
}
