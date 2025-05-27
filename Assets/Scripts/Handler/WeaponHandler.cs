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
    public GameObject CurrentWeapon => current;
    public WeaponType currentType { get; private set; }

    public void EquipNext()
    {
        if (current) Destroy(current);

        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null) return;


        currentType = data.type; // <-- ESSA LINHA É ESSENCIAL!
        current = Instantiate(swordBasePrefab, handBone);

        current.transform.localPosition = positionOffset + new Vector3(0, 0, zOffset);
        current.transform.localEulerAngles = rotationOffset;
        current.transform.localScale = Vector3.one * data.scale;

        var sr = current.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }
}
