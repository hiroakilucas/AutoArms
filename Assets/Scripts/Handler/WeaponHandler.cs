using UnityEngine;

public class WeaponHandler : MonoBehaviour
{
    public PlayerLoadout loadout;
    public GameObject swordBasePrefab;
    public Transform handBone;
    public Vector3 positionOffset, rotationOffset;
    public float zOffset;
    public string sortingLayer = "Weapon";
    public int sortingOrder = 0;

    private GameObject current;
    public WeaponType currentType { get; private set; }

    public void EquipNext()
    {
        if (current) Destroy(current);
        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null) return;

        currentType = data.type;           // armazena o tipo
        current = Instantiate(swordBasePrefab, handBone);

        current = Instantiate(swordBasePrefab, handBone);
        current.transform.localPosition = positionOffset + new Vector3(0, 0, zOffset);
        current.transform.localEulerAngles = rotationOffset;

        var sr = current.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }
}
