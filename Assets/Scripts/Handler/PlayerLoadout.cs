using UnityEngine;
using System.Collections.Generic;

public class PlayerLoadout : MonoBehaviour
{
    public WeaponLoadout loadout;
    [HideInInspector] public int currentIndex = -1;

    // Runtime copy so weapons can be permanently removed without mutating the ScriptableObject.
    // Lazily initialized on first use so CombatSceneLoader can assign loadout.loadout before we read it.
    private List<WeaponData> runtimeWeapons;

    private void EnsureRuntime()
    {
        if (runtimeWeapons == null)
            runtimeWeapons = new List<WeaponData>(loadout?.weapons ?? new WeaponData[0]);
    }

    public WeaponData GetNextWeapon()
    {
        EnsureRuntime();
        if (runtimeWeapons.Count == 0) return null;
        currentIndex = (currentIndex + 1) % runtimeWeapons.Count;
        return runtimeWeapons[currentIndex];
    }

    // Permanently removes the weapon at currentIndex from this combat's loadout.
    // Validates that the slot still holds `expected` before removing — guards against index drift.
    // Does not affect the ScriptableObject asset.
    public void RemoveCurrentWeapon(WeaponData expected = null)
    {
        EnsureRuntime();
        if (currentIndex < 0 || currentIndex >= runtimeWeapons.Count) return;
        if (expected != null && runtimeWeapons[currentIndex] != expected) return;
        runtimeWeapons.RemoveAt(currentIndex);
        currentIndex--;
    }
}
