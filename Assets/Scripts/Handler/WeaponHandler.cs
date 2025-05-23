using UnityEngine;

public class WeaponHandler : MonoBehaviour
{
    public PlayerLoadout loadout;
    public GameObject swordBasePrefab;
    public Transform handBone;
    public Vector3 positionOffset, rotationOffset;
    public float zOffset;
    public string sortingLayer;
    public int sortingOrder;

    private GameObject current;

    public void EquipNext()
    {
        if (current) Destroy(current);
        var data = loadout.GetNextWeapon();
        if (data?.inHandSprite == null) return;

        current = Instantiate(swordBasePrefab, handBone);
        current.transform.localPosition = positionOffset + new Vector3(0, 0, zOffset);
        current.transform.localEulerAngles = rotationOffset;

        var sr = current.GetComponent<SpriteRenderer>();
        sr.sprite = data.inHandSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
    }
}
