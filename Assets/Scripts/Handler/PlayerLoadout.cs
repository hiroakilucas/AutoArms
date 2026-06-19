using UnityEngine;
using System.Collections.Generic;

public class PlayerLoadout : MonoBehaviour
{
    public WeaponLoadout loadout;
    [HideInInspector] public int currentIndex = -1;

    // Fires whenever a weapon is permanently removed from the runtime list.
    public event System.Action OnWeaponsChanged;

    public int CurrentIndex => currentIndex;

    // Read-only view of the runtime weapon list; initializes lazily on first access.
    public IReadOnlyList<WeaponData> Weapons { get { EnsureRuntime(); return runtimeWeapons; } }

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

    public WeaponData GetRandomWeapon()
    {
        EnsureRuntime();
        if (runtimeWeapons.Count == 0) return null;
        currentIndex = Random.Range(0, runtimeWeapons.Count);
        return runtimeWeapons[currentIndex];
    }

    // Permanently removes `expected` from this combat's loadout. Looks it up by reference
    // instead of trusting currentIndex — CombatPlayer equips weapons via WeaponHandler.EquipSpecific
    // (to match whatever CombatSimulator picked), which never advances currentIndex, so it stays
    // stuck at -1 in the simulator-driven path. Falling back to currentIndex only when no
    // `expected` is given keeps the old cycling behavior (EquipNext/EquipRandom) working too.
    // Does not affect the ScriptableObject asset.
    public void RemoveCurrentWeapon(WeaponData expected = null)
    {
        EnsureRuntime();
        int idx = expected != null ? runtimeWeapons.IndexOf(expected) : currentIndex;
        if (idx < 0 || idx >= runtimeWeapons.Count) return;
        runtimeWeapons.RemoveAt(idx);
        if (idx <= currentIndex) currentIndex--;
        OnWeaponsChanged?.Invoke();
    }

    // Adiciona uma arma ao loadout em runtime (ex.: arma roubada pela skill Thief) — dispara
    // OnWeaponsChanged pra WeaponHUD mostrar o ícone novo, mesmo padrão de RemoveCurrentWeapon.
    // Não afeta o ScriptableObject (mesma garantia de runtimeWeapons já documentada acima).
    public void AddWeapon(WeaponData weapon)
    {
        EnsureRuntime();
        runtimeWeapons.Add(weapon);
        OnWeaponsChanged?.Invoke();
    }
}
