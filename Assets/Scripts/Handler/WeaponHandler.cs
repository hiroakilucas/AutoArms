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
    public string sortingLayer = "Weapons";
    public int sortingOrder = 0;

    private GameObject current;
    public GameObject CurrentWeapon => current;
    public WeaponType currentType { get; private set; }
    public WeaponData CurrentWeaponData { get; private set; }

    // Fires with the newly equipped WeaponData, or null when unequipped.
    public event System.Action<WeaponData> OnWeaponChanged;

    public void EquipNext()
    {
        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null)
        {
            Unequip();
            return;
        }

        if (current) Destroy(current);
        EquipData(data);
    }

    public void EquipRandom()
    {
        var data = loadout.GetRandomWeapon();
        if (data?.inHandSprite == null)
        {
            Unequip();
            return;
        }

        if (current) Destroy(current);
        EquipData(data);
    }

    // Equipa uma WeaponData específica (em vez de sortear) — usado pelo CombatPlayer para
    // que a arma exibida visualmente seja sempre a mesma que o CombatSimulator usou no cálculo
    // de dano daquele evento, em vez de um sorteio independente que podia divergir.
    public void EquipSpecific(WeaponData data)
    {
        if (data?.inHandSprite == null)
        {
            Unequip();
            return;
        }

        if (current) Destroy(current);
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

        OnWeaponChanged?.Invoke(data);
    }

    public void Unequip()
    {
        if (current) Destroy(current);
        current = null;
        currentType = default;
        CurrentWeaponData = null;
        OnWeaponChanged?.Invoke(null);
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
