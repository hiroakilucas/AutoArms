using UnityEngine;

public class PlayerLoadout : MonoBehaviour
{
    public WeaponLoadout loadout;
    // Começa em -1 para que a primeira chamada
    // a GetNextWeapon() seja weapons[0].
    [HideInInspector] public int currentIndex = -1;

    public WeaponData GetNextWeapon()
    {
        if (loadout == null || loadout.weapons.Length == 0) return null;

        currentIndex = (currentIndex + 1) % loadout.weapons.Length;
        var w = loadout.weapons[currentIndex];
        Debug.Log($"[PlayerLoadout] Equipando índice {currentIndex}: {w.type}");
        return w;
    }
}
