using UnityEngine;

public class PlayerLoadout : MonoBehaviour
{
    public WeaponLoadout loadout;
    [HideInInspector] public int nextWeaponIndex = 0;

    public WeaponData GetNextWeapon()
    {
        if (loadout == null || loadout.weapons.Length == 0) return null;
        var w = loadout.weapons[nextWeaponIndex];
        nextWeaponIndex = (nextWeaponIndex + 1) % loadout.weapons.Length;
        return w;
    }
}
