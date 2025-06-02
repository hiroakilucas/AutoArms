using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerProfile", menuName = "Game/Player Profile", order = 100)]
public class PlayerProfile : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Nome do personagem para exibição")]
    public string profileName;

    [Header("Prefab e Imagem")]
    [Tooltip("Prefab do personagem (deve conter todos os componentes de combate)")]
    public GameObject characterPrefab;

    [Tooltip("Ícone para UI de seleção")]
    public Sprite previewIcon;

    [Header("Parâmetros de Combate")]
    [Tooltip("ScriptableObject com as configurações de ataque")]
    public AttackSettings attackSettings;

    [Tooltip("ScriptableObject com as armas que esse personagem vai usar")]
    public WeaponLoadout weaponLoadout;

    [Header("Instanciação")]
    [Tooltip("Escala personalizada do personagem (opcional, se diferente de 1)")]
    public Vector3 scale = Vector3.one;

    [Tooltip("Posição inicial para o combate (usado no PvP)")]
    public Vector2 startPos = new Vector2(-6.3f, -2.407897f);
}
