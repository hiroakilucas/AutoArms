using UnityEngine;

// Tier T1/T2/T3 para pets (2026-07-16), mesmo espírito de SkillData/WeaponData — um asset por
// tier (pet_mouse_t1.asset/_t2/_t3, ver PetTierGenerator.cs), tier/previousTier/nextTier pra
// navegar a cadeia (mesmo padrão de WeaponData, que tem os dois sentidos). Campos NOMEADOS em
// vez de bonusValue1-7 (padrão de SkillData) — pets precisam de mais valores distintos (até 13
// entre stats "core" e bônus especiais) do que os 7 slots genéricos comportam sem ambiguidade.
//
// Nem todo campo é usado por todo pet — cada um tem seus bônus especiais próprios (ver tabela
// aprovada pelo usuário, 2026-07-16): Rato só usa comboRate; Macaco usa evasionBase+comboRate;
// Javali usa evasionBase+accuracyBonus+disarmRate+comboDebuff+blockDebuff (os 2 últimos são
// penalidades FIXAS aplicadas ao OPONENTE, não escalam por tier — ver CombatSimulator). Campos
// não usados por um pet ficam 0 (mesmo padrão de WeaponData com seus muitos bônus condicionais).
[CreateAssetMenu(fileName = "NewPetData", menuName = "Game/Pet Data", order = 102)]
public class PetData : ScriptableObject
{
    [Header("Identificação")]
    public PetType petType;

    [Header("Tier")]
    public int tier = 1;
    public PetData previousTier;
    public PetData nextTier;

    [Header("Visual")]
    public Sprite icon;

    [Header("Custo e chance (fixos por TIPO de pet — não escalam por tier)")]
    [Tooltip("% do HP máximo BASE do personagem, cobrado só na 1ª vez que esse tipo de pet é escolhido (T1). Evoluir pra T2/T3 não cobra de novo.")]
    public float hpMalusPercent;

    [Tooltip("Chance deste pet aparecer como opção no level-up — campo reservado, ainda NÃO conectado no sorteio (LevelUpEngine.DrawOption). Ver CLAUDE.md/Fase 3.")]
    public float odds;

    [Tooltip("Decide a ordem entre os PRÓPRIOS pets do mesmo dono quando há mais de um tipo — maior valor age primeiro (CombatSimulator.SimulatePetActions).")]
    public float initiative;

    [Header("Atributos (escalam por tier)")]
    public float str;
    public int agility;
    public int speed;
    public int hp;
    public int damage;

    [Header("Bônus especiais do PRÓPRIO pet (variam por pet, escalam por tier)")]
    [Tooltip("Chance de golpe extra (combo) do próprio pet. Javali sempre 0 (nunca combina).")]
    public float comboRate;
    [Tooltip("Chance de esquiva do próprio pet. Rato sempre 0 (sem esquiva própria).")]
    public float evasionBase;
    [Tooltip("Reduz a chance de esquiva do PERSONAGEM alvo contra os ataques deste pet (só Javali).")]
    public float accuracyBonus;
    [Tooltip("Chance deste pet desarmar o personagem alvo, só no 1º hit do turno (só Javali).")]
    public float disarmRate;

    [Header("Penalidades no OPONENTE (fixas — não escalam por tier, só Javali)")]
    [Tooltip("Debuff fixo (negativo) na % de Combo do OPONENTE enquanto este Javali estiver vivo — não afeta o combo do próprio Javali (já é 0).")]
    public float comboDebuff;
    [Tooltip("Debuff fixo (negativo) na % de Block do OPONENTE enquanto este Javali estiver vivo.")]
    public float blockDebuff;
}
