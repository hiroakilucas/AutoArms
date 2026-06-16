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

    [Header("My Brute — Propriedades da Arma")]
    public float hitSpeed = 1.0f;            // multiplicador de velocidade de ataque
    public float drawChance = 0f;            // % chance de pegar esta arma ao pick up
    public int   reach = 0;                  // alcance — soma à distância base de AttackPosition
    public float critChanceBonus = 0f;
    public float critDamageMultiplier = 1.0f; // multiplicador de dano crítico
    public float evasionBonus = 0f;          // bônus de esquiva ao segurar esta arma
    public float dexterityBonus = 0f;        // bônus de precisão
    public float reversalBonus = 0f;
    public float blockBonus = 0f;
    public float accuracyBonus = 0f;
    public float disarmBonus = 0f;
    public float comboBonus = 0f;
    public float deflectBonus = 0f;
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

// Propriedades do combate desarmado (sem WeaponData associado) — valores do My Brute.
public static class UnarmedStats
{
    public const float HitSpeed = 1.0f;
    public const int   Damage = 5;
    public const float CritChanceBonus = 0.05f;
    public const float CritDamageMultiplier = 1.5f;
    public const float ReversalBonus = 0f;
    public const float EvasionBonus = 0.10f;
    public const float DexterityBonus = 0.20f;
    public const float BlockBonus = -0.25f;
    public const float AccuracyBonus = 0f;
    public const float DisarmBonus = 0.05f;
    public const float ComboBonus = 0f;
    public const float DeflectBonus = 0f;
}
