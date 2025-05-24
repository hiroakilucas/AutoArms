using UnityEngine;

public class WeaponHandler : MonoBehaviour
{
    [Header("Loadout e Prefab")]
    public PlayerLoadout loadout;
    public GameObject swordBasePrefab;

    [Header("Pivô e Offset")]
    public Transform handBone;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float zOffset;

    [Header("Renderização")]
    public string sortingLayer = "Weapon";
    public int sortingOrder = 0;

    private GameObject current;
    public WeaponType currentType { get; private set; }

    public void EquipNext()
    {
        // Remove arma anterior
        if (current) Destroy(current);

        // Pega dados da próxima arma
        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null) return;

        // Armazena tipo e instancia o prefab em handBone
        currentType = data.type;
        current = Instantiate(swordBasePrefab, handBone);

        // Aplica posição, rotação e escala conforme o data.scale
        current.transform.localPosition = positionOffset + new Vector3(0, 0, zOffset);
        current.transform.localEulerAngles = rotationOffset;
        current.transform.localScale = Vector3.one * data.scale;

        // Ajusta sprite e ordem de renderização
        var sr = current.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }
}
