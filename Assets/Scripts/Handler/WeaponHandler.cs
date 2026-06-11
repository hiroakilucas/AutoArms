using UnityEngine;

public class WeaponHandler : MonoBehaviour
{
    [Header("Loadout e Prefab")]
    public PlayerLoadout loadout;
    public GameObject swordBasePrefab;

    [Header("Pivo e Offset")]
    public Transform handBone;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float zOffset;

    [Header("Renderizacao")]
    public string sortingLayer = "Weapon";
    public int sortingOrder = 0;

    private GameObject current;
    public GameObject CurrentWeapon => current;
    public WeaponType currentType { get; private set; }
    public WeaponData CurrentWeaponData { get; private set; }

    public void EquipNext()
    {
        if (current) Destroy(current);

        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null) return;

        EquipData(data);
    }

    public void EquipRandom()
    {
        if (current) Destroy(current);

        var data = loadout.GetRandomWeapon();
        if (data?.inHandSprite == null) return;

        EquipData(data);
    }

    private void EquipData(WeaponData data)
    {
        currentType = data.type;
        CurrentWeaponData = data;
        current = Instantiate(swordBasePrefab, handBone);

        current.transform.localPosition = positionOffset + new Vector3(0, 0, zOffset);
        current.transform.localEulerAngles = rotationOffset;
        current.transform.localScale = Vector3.one * data.scale;

        var sr = current.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }

    public void Unequip()
    {
        if (current) Destroy(current);
        current = null;
        currentType = default;
        CurrentWeaponData = null;
    }

    // Unequip and permanently remove this weapon from the runtime loadout for this combat.
    // Captures CurrentWeaponData BEFORE Unequip() clears it, then passes it to RemoveCurrentWeapon
    // so the loadout can verify it's removing the correct slot.
    public void UnequipPermanent()
    {
        var data = CurrentWeaponData;
        Unequip();
        if (data != null)
            loadout?.RemoveCurrentWeapon(data);
    }
}
