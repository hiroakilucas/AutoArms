using UnityEngine;

// Extraído de CombatResultPanel (era um struct privado aninhado) pra ser compartilhado entre a
// tela real de level-up (CombatResultPanel) e o gerador de bots (BotProfileGenerator/LevelUpEngine)
// — ambos precisam do mesmo vocabulário de "uma opção de bônus de level-up".
public struct LevelUpOption
{
    public enum Kind { Attribute, Skill, Weapon, Pet }
    public Kind kind;
    public int attrIndex;
    public SkillData skill;
    public WeaponData weapon;
    public PetData petData;

    public string Name() => kind switch {
        Kind.Attribute => new[] {
            "+12 HP", "+2 STR", "+2 AGI", "+2 SPD",
            "+1 STR / +1 AGI", "+1 AGI / +1 SPD", "+1 STR / +1 SPD",
            "+6 HP / +1 STR", "+6 HP / +1 AGI", "+6 HP / +1 SPD"
        }[attrIndex],
        Kind.Skill     => skill != null ? (skill.tier > 1 ? $"{skill.skillName} (T{skill.tier})" : skill.skillName) : "?",
        Kind.Weapon    => weapon?.weaponName ?? "?",
        Kind.Pet       => petData != null ? PetState.DisplayName(petData.petType) : "?",
        _              => "?"
    };

    public string Desc() => kind switch {
        Kind.Attribute => new[] {
            "Vida máxima +12",
            "Força +2",
            "Agilidade +2",
            "Velocidade +2",
            "Força +1, Agilidade +1",
            "Agilidade +1, Velocidade +1",
            "Força +1, Velocidade +1",
            "Vida máxima +6, Força +1",
            "Vida máxima +6, Agilidade +1",
            "Vida máxima +6, Velocidade +1"
        }[attrIndex],
        Kind.Skill  => skill?.description ?? "",
        Kind.Weapon => weapon != null ? $"{string.Join(", ", weapon.types)} • {weapon.damage} dano" : "",
        // -% do HP BASE do dono, só na 1ª aquisição (T1) — ApplyOption já garante maxHealth >= 1.
        // Evoluir pra T2/T3 (mesmo tipo escolhido de novo) não cobra HP de novo.
        Kind.Pet    => petData != null
            ? (petData.tier == 1
                ? $"Luta junto. -{petData.hpMalusPercent:P0} HP máximo do dono"
                : $"Evolui pro Tier {petData.tier}. Sem custo de HP adicional.")
            : "",
        _           => ""
    };

    public Color AttrColor() => kind == Kind.Attribute ? attrIndex switch {
        0 => new Color(0.8f, 0.2f, 0.2f),
        1 => new Color(1f,   0.6f, 0.1f),
        2 => new Color(0.2f, 0.7f, 0.2f),
        3 => new Color(0.3f, 0.5f, 1f),
        4 => new Color(0.9f, 0.7f, 0.1f),
        5 => new Color(0.2f, 0.8f, 0.6f),
        6 => new Color(0.7f, 0.4f, 0.9f),
        7 => new Color(0.9f, 0.4f, 0.2f),  // HP+STR — laranja-avermelhado
        8 => new Color(0.4f, 0.7f, 0.3f),  // HP+AGI — verde-médio
        9 => new Color(0.4f, 0.6f, 0.9f),  // HP+SPD — azul-médio
        _ => Color.white
    } : Color.white;
}
