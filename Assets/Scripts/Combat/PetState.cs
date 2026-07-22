// Mutable snapshot of one pet's state inside CombatSimulator. Pure C# — no MonoBehaviour,
// mesmo padrão do PlayerState. Cada PetData do PlayerProfile.pets vira uma instância
// independente (sem restrição de duplicatas — 3 Ratos geram 3 PetState separados).
//
// Tiers T1/T2/T3 (2026-07-16) — stats vêm de um PetData (ScriptableObject) em vez do switch(type)
// hardcoded de antes; ver PetTierGenerator.cs pra tabela de valores. `counter`/`reversal` do
// Macaco (mecânica antiga) foram removidos por completo — não fazem parte da tabela nova
// aprovada pelo usuário.
public class PetState
{
    public PetType type;
    public int   tier;
    public int   hp;
    public int   maxHp;
    public float str;
    public int   agility;
    public int   speed;
    public float comboRate;
    public float disarmRate;
    public float evasionBase;
    public float accuracyBonus;
    public float comboDebuff;
    public float blockDebuff;

    // false quando hp <= 0 — campo setado explicitamente em CombatSimulator.ApplyDamageToPet.
    // Pet cai e FICA NO LUGAR (GameObject nunca destruído) — carcaça disponível pro Tamer.
    public bool isAlive = true;

    // true após ser comido pelo Tamer — não pode ser consumido duas vezes.
    public bool isConsumed;

    // Escudo do Treat — imune ao próximo ataque recebido (1 golpe); removido ao absorver.
    public bool shielded;

    // Preso em Net — PERMANENTE para pets (nunca volta a false, diferente de
    // PlayerState.netEnsnared, que se solta no próximo hit que o personagem sofrer).
    public bool netEnsnared;

    // Chef NUNCA atinge pets — campos preparados por paridade com PlayerState, mas nenhum
    // código do simulador os seta de verdade para pets ainda.
    public bool poisoned;
    public int  poisonDamagePerTurn;

    // Contador de iniciativa do sistema ATB, próprio do pet (independente do speedDebt do
    // dono) — soma `speed` a cada tick, dispara uma ação ao cruzar
    // CombatSettings.initiativeThreshold. Ver CombatSimulator.RunInitiativeLoop.
    public int speedDebt;

    // Lê todos os stats de combate do PetData (tier já resolvido no asset) — substitui o antigo
    // switch(PetType) hardcoded. `data` pode ser null (chamador decide o que fazer, mesmo padrão
    // de tolerância a null do resto do arquivo).
    public static PetState Create(PetData data)
    {
        if (data == null) return null;
        return new PetState
        {
            type = data.petType,
            tier = data.tier,
            hp = data.hp, maxHp = data.hp,
            str = data.str, agility = data.agility, speed = data.speed,
            comboRate = data.comboRate,
            disarmRate = data.disarmRate,
            evasionBase = data.evasionBase,
            accuracyBonus = data.accuracyBonus,
            comboDebuff = data.comboDebuff,
            blockDebuff = data.blockDebuff,
        };
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
    // é fixo, perdido de uma vez ao escolher o pet — ver PetData.hpMalusPercent/LevelUpEngine).
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
    // comboDelay*2 em PetCombatController.PlayAttackHit. Boar ajustado pra 0.85s a pedido
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
