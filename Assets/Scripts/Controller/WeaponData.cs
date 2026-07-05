using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public Sprite icon;           // para uso em UI mais tarde
    public Sprite inHandSprite;   // sprite que ficará na mão (pose parada/idle)
    // 2º frame opcional (ex: Whip aberto/estalando) — se preenchido, WeaponHandler.SetAttackPose
    // troca pra este sprite durante o swing de ataque e volta pro inHandSprite ao terminar.
    // Deixado null em qualquer arma com sprite único (comportamento de sempre, sem mudança).
    public Sprite attackSprite;
    // Multiplicador aplicado em cima de `scale` só enquanto attackSprite está visível (ex: Whip
    // esticando na largura ao estalar). (1,1,1) = sem mudança nenhuma (default, não afeta
    // nenhuma arma que não configure isso).
    public Vector3 attackScaleMultiplier = Vector3.one;
    // Graus somados ao rotationOffset do WeaponHandler (por personagem) só enquanto attackSprite
    // está visível — ex: Whip apontando um pouco pra baixo, na direção do pé do defensor.
    // (0,0,0) = sem mudança (default).
    public Vector3 attackRotationOffset = Vector3.zero;
    // Efeito visual (faísca/burst) na ponta da arma, só durante o attackSprite — desligado por
    // padrão (false = nenhuma arma nova ganha isso sem configurar). attackTipOffset é a posição
    // da ponta no espaço LOCAL da arma (antes de escala/rotação), usado por
    // WeaponHandler.GetAttackTipWorldPosition().
    public bool showAttackTipEffect = false;
    public Vector3 attackTipOffset = Vector3.zero;
    public int damage;
    public float speedModifier;

    // Lista em vez de [Flags] enum — o Inspector do Unity não tem um jeito limpo de esconder
    // os valores automáticos None/Everything que [Flags] gera no dropdown de máscara. Com uma
    // lista, o usuário adiciona manualmente cada tag (elemento 0, 1, 2...) — até 3 por arma,
    // ver OnValidate abaixo.
    public List<WeaponType> types = new List<WeaponType>();
    [Min(0.01f)]
    public float scale = 1f;      // tamanho arma

    // Bumerangue: em vez de ficar desarmado depois do arremesso (padrão de qualquer arma
    // Thrown), a arma voa até o defensor e volta pra mão do atacante, permanecendo equipada
    // até ele trocar de arma ou ser desarmado/soltar. Ver CombatSimulator.SimulateThrow e
    // CombatPlayer (case BoomerangReturn).
    public bool isBoomerang = false;

    [Header("Animação de Ataque")]
    public AttackAnimation attackAnimation = AttackAnimation.Auto;

    [Header("Tier / Progressão")]
    public int tier = 1;                    // 1, 2 ou 3 — indica o nível de evolução desta arma
    public WeaponData previousTier = null;  // referência para o tier anterior (T2→T1, T3→T2)

    [Header("My Brute — Propriedades da Arma")]
    public float dropOdds = 0f;              // % de chance de drop no level-up
    public float hitSpeed = 1.0f;            // multiplicador de velocidade de ataque
    public float drawChance = 0f;            // % chance de pegar esta arma ao pick up
    public float reach = 0f;                 // alcance — soma à distância base de AttackPosition
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
    // Contra-ataque: defensor bate ANTES do golpe do atacante conectar, cancelando o hit (e o
    // resto do combo) — mesmo mecanismo de PlayerState.counter (skills Monk/Sixth Sense), só que
    // como bônus por arma. Ainda sem efeito em nenhuma fórmula (CounterChance), reservado pra uso
    // futuro — pedido do usuário: adicionar o campo em todas as armas, todas em 0 por enquanto.
    public float counterBonus = 0f;

    // Checa se esta arma carrega a tag `flag` — uma arma pode ter até 3 tags simultâneas na
    // lista `types` (ex: Halberd = Long+Heavy+Sharp).
    public bool HasType(WeaponType flag) => types != null && types.Contains(flag);
    public static bool HasType(WeaponData data, WeaponType flag) => data != null && data.HasType(flag);

    // Usado por Weapon Master (+50% dano com arma "sharp").
    public bool IsSharp() => HasType(WeaponType.Sharp);
    public static bool IsSharp(WeaponData data) => data != null && data.IsSharp();

    // Usado por Lead Skeleton (-15% dano recebido de arma blunt).
    public bool IsBlunt() => HasType(WeaponType.Blunt);
    public static bool IsBlunt(WeaponData data) => data != null && data.IsBlunt();

    // Limite informal de 3 tags por arma (espelha o My Brute original, ex: Halberd = 3 tags) —
    // só avisa no Inspector, não força/limpa automaticamente (o usuário escolhe qual remover).
    private void OnValidate()
    {
        if (types != null && types.Count > 3)
            Debug.LogWarning($"[WeaponData] {name}: {types.Count} tags em WeaponType (máximo recomendado: 3).");
    }
}

// Qual trigger do Animator usar no ataque corpo-a-corpo.
// Auto = comportamento atual (Fast tag → SlashingDagger, todo o resto → Slashing).
// Nota: Throwing como ataque melee exige transições extras no Animator Controller — deixar Auto
// ou Slashing/SlashingDagger por enquanto; Throwing é usado automaticamente no evento ThrowWeapon.
public enum AttackAnimation
{
    Auto,           // deriva da tag: Fast → SlashingDagger, demais → Slashing
    Slashing,       // swing pesado (espada/machado/maça)
    SlashingDagger, // golpe rápido (faca/sai/shuriken)
}

// Tipos do My Brute original. Uma arma pode combinar até 3 (ex: Halberd = Long+Heavy+Sharp,
// Trombone = Heavy+Blunt) via WeaponData.types (List<WeaponType>, não [Flags] bitmask — o
// Inspector do Unity não some os valores automáticos None/Everything que [Flags] gera no
// dropdown de máscara). Substituiu o enum exclusivo antigo (Sword/Heavy/Dagger/Fast/Slow/
// Thrown/Block) — Sword e Dagger se fundiram em Sharp (a distinção "adaga vs espada" agora
// vem de combinar Sharp com Fast ou não); Slow e Block foram removidos (Slow não tinha asset
// usando; Block não é um tipo de arma, era uma categoria antiga de shield).
public enum WeaponType
{
    None   = 0,
    Sharp,
    Blunt,
    Long,
    Heavy,
    Fast,
    Thrown,
    Ranged,
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
    public const float CounterBonus = 0f;
}
