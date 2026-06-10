using UnityEngine;

public class PlayerLoadout : MonoBehaviour
{
    public WeaponLoadout loadout;
    [HideInInspector] public int currentIndex = -1;

    public WeaponData GetNextWeapon()
    {
        if (loadout == null || loadout.weapons.Length == 0) return null;
        currentIndex = (currentIndex + 1) % loadout.weapons.Length;
        return loadout.weapons[currentIndex];
    }
}
