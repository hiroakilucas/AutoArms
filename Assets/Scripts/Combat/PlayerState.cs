using System.Collections.Generic;

// Mutable snapshot of one combatant's state inside CombatSimulator.
// Pure C# — no MonoBehaviour. Used only during pre-calculation.
public class PlayerState
{
    public int    index;
    public string name;

    // Stats (copied from PlayerProfile, then modified by ApplySkillStats)
    public int   hp;
    public int   maxHp;
    public int   str;
    public int   agility;
    public int   speed;
    public int   initiative;
    public float armor;
    public float evasion;
    public float accuracy;
    public float counter;
    public float reversal;
    public float blockBonus;
    public float reversalAfterBlock;
    public float criticalChance;
    public float critDamageBonus;
    public float comboChanceBonus;
    public float disarmChanceBonus;
    public float hitSpeed;
    public float runSpeedMultiplier;
    public float stickyHands;

    // Skill state flags
    public bool leadSkeleton;
    public bool firstHitAvoided;
    public bool noEvasion;
    public bool martialArts;
    public bool weaponsMaster;
    public bool survivalUsed;
    public bool hasShield;
    public int  thiefUsesRemaining = 2;
    public int  flashFloodUsesRemaining = 1;
    public int  hasteUsesRemaining = 1;
    public int  piledriverUsesRemaining = 1;
    public int  netUsesRemaining = 1;
    public int  fierceBruteUsesRemaining = 1;
    public bool fierceBruteActive;
    public int  bombUsesRemaining = 2;
    public int  tragicPotionUsesRemaining = 1;
    public int  vampirismUsesRemaining = 1;
    public int  cryOfTheDamnedUsesRemaining = 2;
    public int  hypnosisUsesRemaining = 1;
    public int  tamerUsesRemaining = 4; // pode comer até 4 carcaças por luta

    // Fast Metabolism — regeneração passiva de 1%/turno (sem campos próprios, sempre ativa
    // enquanto HasSkill for true) + burst de cura intensa (10x 5%, todas no mesmo turno) ao
    // cruzar 50% HP.
    public bool fastMetabolismPulseActive;
    public int  fastMetabolismPulseCount;
    public bool fastMetabolismTookDamage;

    public int  chainHitStreak;
    public int  stunnedActions;
    public bool netEnsnared;

    // Saboteur (do oponente): true enquanto a 1ª arma desta luta ainda não foi quebrada — ver
    // checagem em CombatSimulator.SimulateTurn, logo depois do bloco de Pegar Arma/Thief/Swap.
    public bool saboteurPending;

    // Chef: pizza envenenada lançada na 1ª ação do dono da skill (1x por luta,
    // chefPizzaThrown) — marca o ADVERSÁRIO como poisoned = true e calcula
    // poisonDamagePerTurn (1% do maxHp DELE, arredondado pra cima) no momento do lançamento,
    // já que o defensor só é conhecido em runtime (ver CombatSimulator.SimulateTurn). Tragic
    // Potion cura (poisoned = false) ao ativar — ver TryActivateTragicPotion.
    public bool poisoned;
    public int  poisonDamagePerTurn;
    public bool chefPizzaThrown;

    // Pets (Fase 3, roadmap — ainda não implementados): quando o alvo enredado for um pet,
    // este campo é setado true em vez de netEnsnared sozinho, e SimulateTurn deve tratar isso
    // como permanente — nunca libera o pet mesmo ao sofrer dano (diferente do oponente normal,
    // que se solta no próximo hit que sofrer). Campo preparado agora, sem lógica de pet ainda
    // (não existe PlayerState de pet pra setar isso de verdade).
    public bool netEnsnaredPermanent;

    // Weapons
    public List<WeaponData> weaponLoadout = new List<WeaponData>();
    public WeaponData       currentWeaponData;

    // Skills (by name, for HasSkill checks)
    public List<string> skills = new List<string>();

    // Speed debt accumulation across rounds
    public int speedDebt;

    // Pets (Fase 3) — instâncias independentes, construídas em BuildState a partir de
    // PlayerProfile.pets. Ver CombatSimulator.SimulatePetActions/SimulatePetTurn.
    public List<PetState> pets = new List<PetState>();

    public bool isAlive => hp > 0;

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s == skillName) return true;
        return false;
    }
}
