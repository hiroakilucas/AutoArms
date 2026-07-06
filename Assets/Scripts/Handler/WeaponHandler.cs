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

    // Arremessos repetidos no mesmo turno (hitSpeed alto, ex: Shuriken 10.0) desequipam e
    // reequipam a arma a cada ciclo (ver CombatPlayer, case ThrowWeapon) — CurrentWeaponData
    // fica null durante o intervalo entre um arremesso e o próximo, e o ícone da WeaponHUD
    // (que destaca com base em CurrentWeaponData) piscava cinza nesse meio-tempo em vez de
    // ficar dourado o tempo todo. CombatPlayer seta este campo com a arma sendo arremessada
    // antes de cada Unequip, pra WeaponHUD continuar destacando mesmo com a mão vazia; limpo
    // no TurnEnd. Pedido explícito do usuário: "hud deve ficar amarelo em todo momento".
    public WeaponData PinnedHudWeapon;

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

    // Animação de 2 frames (ex: Whip fechado/idle vs aberto/atacando) — troca o sprite da arma
    // já na mão pro attackSprite durante o swing (CombatPlayer chama true junto do SetTrigger de
    // Slashing/SlashingDagger, false quando o swing termina). No-op se a arma atual não tiver
    // attackSprite configurado (comportamento de sempre, sprite único parado).
    public void SetAttackPose(bool attacking)
    {
        if (current == null || CurrentWeaponData == null) return;
        var sr = current.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (attacking && CurrentWeaponData.attackSprite != null)
        {
            sr.sprite = CurrentWeaponData.attackSprite;
            // attackScaleMultiplier (ex: Whip esticando na largura ao estalar) — (1,1,1) por
            // padrão, então é um no-op pra qualquer arma que não configure isso.
            current.transform.localScale = Vector3.Scale(
                Vector3.one * CurrentWeaponData.scale, CurrentWeaponData.attackScaleMultiplier);
            // attackRotationOffset soma em cima do rotationOffset normal do personagem (ex: Whip
            // apontando pra baixo, na direção do pé do defensor) — (0,0,0) por padrão, no-op.
            current.transform.localEulerAngles = rotationOffset + CurrentWeaponData.attackRotationOffset;
            return;
        }

        // Volta pro idle — mesmo fallback de tier do EquipSpecific (T2/T3 sem sprite sobem a
        // cadeia previousTier).
        var spriteSource = CurrentWeaponData;
        while (spriteSource != null && spriteSource.inHandSprite == null)
            spriteSource = spriteSource.previousTier;
        if (spriteSource?.inHandSprite != null)
            sr.sprite = spriteSource.inHandSprite;
        current.transform.localScale = Vector3.one * CurrentWeaponData.scale;
        current.transform.localEulerAngles = rotationOffset;
    }

    // Posição mundial da ponta da arma durante o attackSprite — usado por efeitos visuais (ex:
    // faísca do Whip). attackTipOffset é lido no espaço LOCAL da arma (antes de escala/rotação);
    // TransformPoint já aplica a escala/rotação atuais (inclusive attackScaleMultiplier/
    // attackRotationOffset acima), então a ponta acompanha a pose de ataque automaticamente.
    public Vector3 GetAttackTipWorldPosition()
    {
        if (current == null || CurrentWeaponData == null) return handBone != null ? handBone.position : transform.position;
        return current.transform.TransformPoint(CurrentWeaponData.attackTipOffset);
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

    // Limpa o pin de HUD do fim de uma sequência de arremessos (ver PinnedHudWeapon) e força a
    // WeaponHUD a reavaliar o destaque — sem isso o ícone ficava "preso" dourado (stale) até o
    // próximo Unequip/EquipSpecific disparar OnWeaponChanged de novo por outro motivo qualquer.
    public void ClearPinnedHudWeapon()
    {
        PinnedHudWeapon = null;
        OnWeaponChanged?.Invoke(CurrentWeaponData);
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
        // Layer própria ("Accessories", sempre a mais à frente de Characters/Characters2) — não
        // reusa o `sortingLayer`/`sortingOrder` da arma normal (default "Weapons"), que fica
        // ATRÁS do corpo (Weapons < Characters na ordem fixa do projeto, ver Sorting Layers no
        // CLAUDE.md). O escudo é permanente (nunca participa do swap Weapons/Weapons2 de
        // atacante/defensor, ver SetAttackerLayers) — bug real reportado pelo usuário: escudo
        // renderizava atrás do próprio corpo o tempo todo.
        sr.sortingLayerName = ShieldSortingLayer;
        sr.sortingOrder = 0;
    }

    private const string ShieldSortingLayer = "Accessories";

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
