using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public Sprite icon;           // para uso em UI mais tarde
    public Sprite inHandSprite;   // sprite que ficará na mão
    public WeaponType type;            // tipo da arma
    public int damage;
    public float speedModifier;
}
public enum WeaponType
{
    Sword,
    Heavy,
    Dagger
}