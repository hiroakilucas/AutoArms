using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public Sprite icon;           // para uso em UI mais tarde
    public Sprite inHandSprite;   // sprite que ficará na mão
    public int damage;
    public float speedModifier;

    public WeaponType type;            // tipo da arma
    [Min(0.01f)]
    public float scale = 1f;      // tamanho arma
}
public enum WeaponType
{
    Sword   = 0,
    Heavy   = 1,
    Dagger  = 2,
    Fast    = 3,
    Slow    = 4,
    Thrown  = 5,
    Block   = 6,
}