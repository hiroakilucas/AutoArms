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

    // WeaponData.hitSpeed < 100% (armas lentas, ex: Anchor 0.48) acumula aqui turno a turno até
    // fechar 1.0 — ver CombatSimulator.ResolveHitSpeedUnits. Resetado sempre que a arma equipada
    // muda de fato (Thief/PickupWeapon/WeaponSwap), pra não carregar o ritmo de uma arma
    // diferente da atual.
    public float weaponHitSpeedDebt;

    // Skill state flags
    public bool leadSkeleton;
    public bool firstHitAvoided;
    public bool noEvasion;
    public bool martialArts;
    public bool weaponsMaster;
    public bool survivalUsed;
    public bool hasShield;
    // Shield: penalidade no dano causado pelo PRÓPRIO usuário do escudo (trade-off do
    // blockBonus alto) — substitui o antigo "armor +=" (que reduzia dano recebido). Aplicado
    // em CalcDamage sobre o atacante, revertido junto de blockBonus quando o escudo cai.
    public float shieldDamagePenalty;
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
    public int  treatUsesRemaining = 4; // alimenta até 4 pets por luta
    public int  mimicUsesRemaining = 1; // copia a última Super do oponente, 1x por luta

    // Mimic: nome da última Super que ESTE jogador ativou — lido pelo oponente que tem Mimic
    // T1/T2 (sempre copia a mais recente). Setado por cada TryActivate*/SimulateFlashFlood/
    // Haste/Piledriver quando dispara.
    public string lastSuperUsed = "";

    // Mimic T3 ("copia as 3 primeiras skills ativas"): histórico cronológico de TODAS as
    // ativações de Super deste jogador nesta luta (com repetição — cada ativação conta,
    // mesmo repetindo o mesmo Super). Preenchido nos mesmos pontos que setam lastSuperUsed
    // acima. A Nª ativação de Mimic T3 do oponente lê superActivationHistory[N-1].
    public List<string> superActivationHistory = new List<string>();

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

    // Mesmos SkillData reais equipados (paralelo a `skills`, mesma ordem/conteúdo) — permite
    // ler bonusValue1..6 (valores de efeito por tier) sem precisar de acesso a PlayerProfile
    // aqui dentro. Populado por CombatSimulator.BuildState junto de `skills`.
    public List<SkillData> skillAssets = new List<SkillData>();

    // Contador de iniciativa do sistema ATB — soma `speed` a cada tick, dispara uma ação ao
    // cruzar CombatSettings.initiativeThreshold. Ver CombatSimulator.RunInitiativeLoop.
    public int speedDebt;

    // Pets (Fase 3) — instâncias independentes, construídas em BuildState a partir de
    // PlayerProfile.pets. Ver CombatSimulator.RunInitiativeLoop/SimulatePetTurn.
    public List<PetState> pets = new List<PetState>();

    public bool isAlive => hp > 0;

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s == skillName) return true;
        return false;
    }

    // Retorna o SkillData equipado com esse nome (pra ler bonusValue1..6) ou null se o
    // personagem não tiver a skill — mesmo padrão de PlayerCombat.GetSkill(string).
    public SkillData GetSkillData(string skillName)
    {
        if (skillAssets == null) return null;
        foreach (var sk in skillAssets)
            if (sk != null && sk.skillName == skillName) return sk;
        return null;
    }
}
