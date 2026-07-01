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

    [Header("Escudo (Off-Hand) — item visual permanente da skill Shield")]
    [Tooltip("Braço oposto ao handBone (ex: handBone = Left Arm → offHandBone = Right Arm).")]
    public Transform offHandBone;
    public Vector3 shieldPositionOffset;
    public Vector3 shieldRotationOffset;
    public float shieldZOffset;

    private GameObject current;
    public GameObject CurrentWeapon => current;
    public WeaponData CurrentWeaponData { get; private set; }

    private GameObject currentShield;
    public GameObject CurrentShield => currentShield;
    public WeaponData CurrentShieldData { get; private set; }

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
    // Fallback de sprite: se este tier não tem inHandSprite (T2/T3 sem sprite atribuído ainda),
    // sobe a cadeia previousTier até achar um — assim o personagem não aparece desarmado visualmente
    // e CurrentWeaponData reflete a arma real (não null), permitindo SwingTrigger/CalcAttackPosition
    // usar as stats corretas do tier equipado.
    public void EquipSpecific(WeaponData data)
    {
        if (data == null) { Unequip(); return; }

        if (current) { Destroy(current); current = null; }

        // Sobe a cadeia de tiers até encontrar um sprite válido
        var spriteSource = data;
        while (spriteSource != null && spriteSource.inHandSprite == null)
            spriteSource = spriteSource.previousTier;

        CurrentWeaponData = data; // sempre registra a arma real, mesmo sem sprite

        if (spriteSource?.inHandSprite != null)
        {
            current = Instantiate(swordBasePrefab, handBone);
            current.transform.localPosition  = positionOffset + new Vector3(0, 0, zOffset);
            current.transform.localEulerAngles = rotationOffset;
            current.transform.localScale     = Vector3.one * data.scale;

            var sr = current.GetComponent<SpriteRenderer>();
            sr.sprite           = spriteSource.inHandSprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder     = sortingOrder;
        }

        OnWeaponChanged?.Invoke(data);
    }

    private void EquipData(WeaponData data)
    {
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
        CurrentWeaponData = null;
        OnWeaponChanged?.Invoke(null);
    }

    // Escudo da skill Shield: item visual permanente no braço oposto, fora do WeaponLoadout —
    // nunca entra no ciclo de troca de armas (EquipNext/EquipRandom/EquipSpecific não o tocam),
    // por isso usa um slot (currentShield) e um bone (offHandBone) totalmente separados de
    // current/handBone. Equipado uma vez no início do combate (CombatSceneLoader.Initialize)
    // e removido só se a skill for desarmada (CombatPlayer, evento ShieldDisarm).
    public void EquipShield(WeaponData data)
    {
        if (data?.inHandSprite == null || offHandBone == null) return;
        if (currentShield) Destroy(currentShield);

        CurrentShieldData = data;
        currentShield = Instantiate(swordBasePrefab, offHandBone);
        currentShield.transform.localPosition = shieldPositionOffset + new Vector3(0, 0, shieldZOffset);
        currentShield.transform.localEulerAngles = shieldRotationOffset;
        currentShield.transform.localScale = Vector3.one * data.scale;

        var sr = currentShield.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }

    public void RemoveShield()
    {
        if (currentShield) Destroy(currentShield);
        currentShield = null;
        CurrentShieldData = null;
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
