using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon Loadout")]
public class WeaponLoadout : ScriptableObject
{
    public WeaponData[] weapons = new WeaponData[10];
}
