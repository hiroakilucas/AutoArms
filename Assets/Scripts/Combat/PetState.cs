// Mutable snapshot of one pet's state inside CombatSimulator. Pure C# — no MonoBehaviour,
// mesmo padrão do PlayerState. Cada PetType do PlayerProfile.pets vira uma instância
// independente (sem restrição de duplicatas — 3 Ratos geram 3 PetState separados).
public class PetState
{
    public PetType type;
    public int   hp;
    public int   maxHp;
    public float str;
    public int   agility;
    public int   speed;
    public float comboRate;
    public float disarmRate;
    public float evasionBase;
    public float counter;
    public float reversal;

    // false quando hp <= 0 — diferente de PlayerState.isAlive (propriedade computada), aqui é
    // um campo setado explicitamente em CombatSimulator.ApplyDamageToPet: o pet cai e FICA NO
    // LUGAR (GameObject nunca destruído) em vez de desaparecer, preparação pra skill futura
    // Tamer ("comer" pets caídos).
    public bool isAlive = true;

    // Preso em Net — PERMANENTE para pets (nunca volta a false, diferente de
    // PlayerState.netEnsnared, que se solta no próximo hit que o personagem sofrer).
    public bool netEnsnared;

    // Chef NUNCA atinge pets — campos preparados por paridade com PlayerState, mas nenhum
    // código do simulador os seta de verdade para pets ainda.
    public bool poisoned;
    public int  poisonDamagePerTurn;

    // Acúmulo de speed debt do pet, independente do speedDebt do dono — ver
    // CombatSimulator.SimulatePetActions.
    public int speedDebt;

    public static PetState Create(PetType type)
    {
        switch (type)
        {
            case PetType.Mouse:
                return new PetState
                {
                    type = type, hp = 25, maxHp = 25, str = 3f, agility = 8, speed = 10,
                    comboRate = 0.20f, disarmRate = 0f, evasionBase = 0.10f, counter = 0f, reversal = 0f,
                };
            case PetType.Monkey:
                return new PetState
                {
                    type = type, hp = 50, maxHp = 50, str = 5f, agility = 25, speed = 20,
                    comboRate = 0.40f, disarmRate = 0f, evasionBase = 0.35f, counter = 0.15f, reversal = 0.20f,
                };
            case PetType.Boar:
                return new PetState
                {
                    type = type, hp = 110, maxHp = 110, str = 15f, agility = 2, speed = 3,
                    comboRate = 0f, disarmRate = 0.15f, evasionBase = 0.02f, counter = 0f, reversal = 0f,
                };
            default:
                return null;
        }
    }

    // Random.Range(min, max) (do chamador, com max+1 pra inclusivo) — ver tabela em CLAUDE.md:
    // Rato 4-6, Macaco 9-12 (era 6-10, aumentado a pedido do usuário), Javali 18-27.
    public static (int min, int max) DamageRange(PetType type)
    {
        switch (type)
        {
            case PetType.Mouse:  return (4, 6);
            case PetType.Monkey: return (9, 12);
            case PetType.Boar:   return (18, 27);
            default: return (0, 0);
        }
    }

    // HP perdido pelo dono (profile.maxHealth) ao escolher o pet no level-up.
    public static int HpCost(PetType type)
    {
        switch (type)
        {
            case PetType.Mouse:  return 12;
            case PetType.Monkey: return 36;
            case PetType.Boar:   return 48;
            default: return 0;
        }
    }

    // Escala inicial ao instanciar o prefab em cena (CombatSceneLoader) — aumentada a pedido
    // do usuário (era Mouse 0.15/Monkey 0.20/Boar 0.25, pets ficavam pequenos demais em cena).
    public static float Scale(PetType type)
    {
        switch (type)
        {
            case PetType.Mouse:  return 0.30f;
            case PetType.Monkey: return 0.35f;
            case PetType.Boar:   return 0.60f;
            default: return 0.3f;
        }
    }

    // Escalonamento por nível do PERSONAGEM DONO — a cada 5 níveis dele (5, 10, 15...), os
    // stats do pet sobem permanentemente pro resto daquela luta. Chamado 1x na construção do
    // PetState (CombatSimulator.BuildState/CombatSceneLoader.SpawnPets), nunca recalculado
    // depois — não tem relação com level-up de ATRIBUTO/skill/arma do personagem em si, só
    // lê `profile.level`. HP do bônus entra no maxHp do PET, nunca no maxHealth do dono (esse
    // é fixo, perdido de uma vez ao escolher o pet — ver HpCost acima).
    public void ApplyLevelScaling(int ownerLevel)
    {
        int levelTiers = ownerLevel / 5; // divisão inteira == Floor(ownerLevel / 5f) p/ level >= 0
        if (levelTiers <= 0) return;

        switch (type)
        {
            case PetType.Mouse:
                maxHp += 5 * levelTiers;
                speed += 1 * levelTiers;
                break;
            case PetType.Monkey:
                maxHp   += 10 * levelTiers;
                agility +=  2 * levelTiers;
                speed   +=  1 * levelTiers;
                break;
            case PetType.Boar:
                maxHp   += 20 * levelTiers;
                agility +=  1 * levelTiers;
                speed   +=  1 * levelTiers;
                str     +=  3 * levelTiers;
                break;
        }

        hp = maxHp; // full health no início da luta, já com o bônus aplicado
    }

    // Duração total do "swing" de Slashing (pré-impacto + pós-impacto, mesmo papel de
    // AttackSettings.slashingDuration pros personagens) — usada por CombatPlayer como
    // comboDelay*2 em PetCombatController.PlayAttackSequence. Boar ajustado pra 0.85s a pedido
    // do usuário (clip Slashing.anim do Boar tem ~0.83s a 30fps depois do fix de Sample Rate,
    // ver CLAUDE.md); Monkey/Mouse mantêm o default 0.4s já usado antes (sem reclamação
    // específica sobre esses dois).
    public static float SlashDuration(PetType type)
    {
        switch (type)
        {
            case PetType.Boar: return 0.85f;
            default: return 0.4f;
        }
    }

    public static string DisplayName(PetType type)
    {
        switch (type)
        {
            case PetType.Mouse:  return "Rato";
            case PetType.Monkey: return "Macaco";
            case PetType.Boar:   return "Javali";
            default: return "Pet";
        }
    }
}
