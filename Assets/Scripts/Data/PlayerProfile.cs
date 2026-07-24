using UnityEngine;
using System.Collections.Generic;

// Rato (Mouse, equivalente ao Dog), Macaco (Monkey, equivalente ao Panther), Javali
// (Boar, equivalente ao Bear) — ver stats completos (por tier) em PetData.cs/PetTierGenerator.cs.
public enum PetType
{
    None,
    Mouse,
    Monkey,
    Boar,
}

// Raridade do personagem (não confundir com o `level` de progressão/XP abaixo, que continua
// crescendo sem teto) — usada só pra colorir o PortraitBox na seleção de personagem
// (CharacterCardUI), do menos ao mais raro. Cores em UITheme.rarityNormal/Uncommon/Rare/
// Legendary/Immortal (ver UI_PALETTE.md).
public enum CharacterRarity
{
    Normal,
    Uncommon,
    Rare,
    Legendary,
    Immortal,
}

[CreateAssetMenu(fileName = "NewPlayerProfile", menuName = "Game/Player Profile", order = 100)]
public class PlayerProfile : ScriptableObject
{
    [Header("Identifica��o")]
    [Tooltip("Nome do personagem para exibi��o")]
    public string profileName;

    [Tooltip("ID estavel de save (2026-07-14). Vazio nos assets pre-autorados de hoje (sem conta ainda) - OpponentId()/LocalSaveService caem pro nome do asset Unity (name) quando vazio, retrocompativel. Passa a ser preenchido de verdade quando existir personagem por conta (Firestore).")]
    public string characterId = "";

    // Marca instancias runtime (ScriptableObject.CreateInstance, nunca um asset do projeto) -
    // 2026-07-24, sistema de compra de personagens/case opening. Setado por
    // PlayerProfileConverter.FromOpponentIndexMap/FromCharacterDTO logo apos CreateInstance.
    // [NonSerialized] pra nunca vazar num asset serializado (nao faz sentido nele, so existe pra
    // instancias em memoria). Usado por PlayerProfileConverter.CapturePristineIfNeeded/
    // MarkOwnerScope pra pular o rastreamento de "dono"/snapshot de fabrica nessas instancias -
    // sem isso, cada instancia runtime (uma por fetch de oponente/roster) vira uma entrada nova
    // e permanente num Dictionary<PlayerProfile,...> estatico, sem teto (essas instancias nunca
    // sao reaproveitadas entre contas por construcao, entao nao precisam da protecao cross-conta
    // que esses dicionarios existem pra dar aos ~72 assets pre-autorados compartilhados).
    [System.NonSerialized] public bool isRuntimeInstance = false;

    // Nome do asset "molde" de origem (2026-07-25, ver PlayerProfileConverter.FromCharacterDTO) -
    // só preenchido em instâncias runtime concedidas via case opening (personagem de roster, não
    // o "original" da conta). [NonSerialized] pelo mesmo motivo de isRuntimeInstance acima - usado
    // por CharacterSelectController.PopulateCharacterGridRoutine pra NÃO mostrar o card travado do
    // molde (characterDatabase.unlockedCharacters) quando a conta já possui uma instância jogável
    // deste mesmo characterTypeId no roster - sem isso, comprar "Anubis" via case exibia DOIS
    // cards "Anubis" lado a lado (o molde travado + a instância jogável), visualmente idênticos e
    // fáceis de confundir (bug real reportado pelo usuário: "os chibers que eu comprei vieram
    // desabilitados" - na verdade era o molde travado, a instância jogável estava em outro lugar
    // da grade).
    [System.NonSerialized] public string characterTypeId = "";

    [Header("Prefab e Imagem")]
    [Tooltip("Prefab do personagem (deve conter todos os componentes necess�rios para o combate)")]
    public GameObject characterPrefab;

    [Tooltip("Controla se este personagem aparece no grid de 02_SelectCharacter. Perfis j� existentes na hora em que este campo foi criado permanecem true automaticamente (Unity usa o valor do inicializador quando a chave n�o existe ainda no .asset serializado).")]
    public bool isUnlockedForSelection = true;

    [Tooltip("Controla se este personagem fica clic�vel/escolh�vel pra batalhar dentro do grid (diferente de isUnlockedForSelection, que s� controla se ele aparece). false = aparece no grid mas travado/cinza, sem Button. Default false por seguran�a � precisa ser ligado manualmente por personagem, inclusive nos j� existentes.")]
    public bool isPlayable = false;

    [Tooltip("Favoritado pelo jogador (estrela no canto superior direito do card em 02_SelectCharacter) - personagens favoritados aparecem primeiro na grade, antes da ordem alfabetica. Alterado em runtime por CharacterCardUI.OnFavoriteClicked, persistido via LocalSaveService (save real em build) + EditorUtility.SetDirty (so no Editor).")]
    public bool isFavorite = false;

    [Tooltip("�cone utilizado na UI de sele��o de personagens")]
    public Sprite previewIcon;

    [Tooltip("Arte de fundo em tela cheia (splash art) mostrada no Frame ao selecionar este personagem em 02_SelectCharacter. Vazio = mant�m o placeholder dourado.")]
    public Sprite splashArt;

    [Tooltip("Raridade do personagem (Normal/Uncommon/Rare/Legendary/Immortal) — colore o fundo do PortraitBox na sele��o de personagem. N�o tem nenhum efeito em combate.")]
    public CharacterRarity rarity = CharacterRarity.Normal;

    [Header("Par�metros de Combate")]
    [Tooltip("Configura��es de ataque e anima��o (velocidade, idle, delay etc.)")]
    public AttackSettings attackSettings;

    [Tooltip("Armas atribu�das para esse personagem")]
    public List<WeaponData> weapons = new List<WeaponData>();

    [Header("Instancia��o")]
    [Tooltip("Escala personalizada do personagem no momento da inst�ncia")]
    public Vector3 scale = Vector3.one;

    [Tooltip("Posi��o de in�cio no combate (usada no PvP, exemplo: lado esquerdo)")]
    public Vector2 startPos = new Vector2(-6.3f, -2.407897f);

    [Header("Combate")]
    [Tooltip("Vida máxima do personagem")]
    public int maxHealth = 50;

    [Header("Atributos")]
    public int str = 10;
    public int agility = 10;
    public int speed = 10;
    public float armor = 0f;
    public float evasion = 0f;
    public float accuracy = 0f;
    public int initiative = 0;
    public float reversal = 0f;
    public float counter = 0f;
    public float blockBonus = 0f;
    public float reversalAfterBlock = 0f;
    public float criticalChance = 0f;
    public float hitSpeed = 1f;

    [Header("Skills")]
    public List<SkillData> skills = new List<SkillData>();

    // Cada entrada é um PetData (ScriptableObject com tier T1/T2/T3, 2026-07-16 — era
    // List<PetType>, sem tier nenhum) e vira uma instância independente (PetState) com seu
    // próprio HP/estado na luta. Duplicata do mesmo TIPO não é mais permitida a partir do
    // level-up (evolui pro próximo tier em vez de somar uma cópia — ver LevelUpEngine.ApplyOption);
    // esta lista em si continua sendo uma lista comum, só a lógica de escolha impede duplicar tipo.
    [Header("Pets")]
    public List<PetData> pets = new List<PetData>();

    // Unlocks progressivos de skill/arma/pet concedidos ao ganhar este personagem via case
    // opening (2026-07-25) - ver CharacterUnlockEngine. Marca que a sequencia de N sorteios
    // (CharacterUnlockEngine.UnlockCountForRarity) ja rodou por completo pra esta instancia -
    // impede CharacterSelectController.ResolveCaseUnlocksAsync de rodar de novo (e conceder itens
    // duplicados) toda vez que o detalhe deste personagem for reaberto. Persistido via
    // CharacterDTO/CharacterDTOMap (Firestore) - default false pros ~72 assets pre-autorados
    // (nunca passam por esse fluxo, o campo simplesmente nunca importa pra eles).
    public bool caseUnlocksResolved = false;

    // Quantos unlocks já foram ACEITOS ("Continuar" clicado) nesta sequência (2026-07-25) - não
    // dá pra usar skills.Count+weapons.Count+pets.Count como proxy, porque um unlock que evolui
    // uma família já possuída (ex: 2º unlock repete a mesma skill do 1º, virando T2) NÃO aumenta
    // esse total. Usado por CharacterSelectController.ResolveCaseUnlocksAsync como ponto de
    // retomada (`for (i = caseUnlocksAcceptedCount + 1; i <= total; i++)`) - sem isso, fechar o
    // app no meio da sequência (antes de aceitar o 1º unlock, por exemplo) fazia os unlocks
    // restantes se perderem pra sempre (reabrir o personagem depois nunca tentava de novo) ou, se
    // o fluxo reiniciasse do zero sem essa contagem, duplicava os já aceitos. Persistido via
    // CharacterDTO/CharacterDTOMap (Firestore) - default 0 pros ~72 assets pre-autorados (nunca
    // passam por esse fluxo, o campo nunca importa pra eles).
    public int caseUnlocksAcceptedCount = 0;

    // Rascunho do unlock ATUAL, ainda NÃO aceito (2026-07-25, 2ª rodada de bug real corrigido) —
    // fechar o app enquanto um unlock estava sendo revelado (ex: "Book" sorteado, ainda sem
    // clicar "Continuar") sorteava um resultado DIFERENTE ao reabrir ("Vampirismo"), em vez de
    // continuar mostrando o mesmo rascunho — reportado pelo usuário. `pendingUnlockIndex == 0`
    // = nenhum rascunho pendente (unlock ainda nem foi sorteado desta vez, ou já foi aceito).
    // kind/name/tier usam o MESMO shape do servidor (rerollUnlock — "skill"/"weapon"/"pet" +
    // nome de família + tier), resolvido de volta pro LevelUpOption real via
    // CharacterUnlockEngine.ResolveServerResult (funciona igual pra um resultado vindo do
    // servidor ou de um sorteio local, ver CharacterUnlockEngine.ToServerShape).
    public int pendingUnlockIndex = 0;
    public string pendingUnlockKind = "";
    public string pendingUnlockName = "";
    public int pendingUnlockTier = 0;

    // Espelha o contador SERVER-AUTHORITATIVE (`unlockRerollCounts`, ver rerollUnlock.ts) pro
    // MESMO índice de pendingUnlockIndex — sem isso, retomar sempre reiniciava a UI mostrando "2
    // refreshes disponíveis" mesmo que o servidor já tivesse contado uso(s) de uma sessão
    // anterior, causando um "resource-exhausted" inesperado (limite já batido) no refresh
    // seguinte, com o botão ainda aparecendo habilitado. Atualizado toda vez que um refresh bem-
    // sucedido muda o rascunho.
    public int pendingUnlockRerollsUsed = 0;

    // Escolha de level-up de COMBATE ainda NÃO confirmada (2026-07-25, bug real corrigido —
    // fechar o app com a tela de "Escolha 1 bônus" aberta perdia XP/level/battlesRemaining/bônus
    // inteiros, sem chance de retomar; ver AttackSequencer.OnCombatEnd/CombatResultPanel).
    // Sistema DIFERENTE do unlock do case opening acima — este é o sorteio de skill/arma/pet/
    // status que acontece a cada level-up em combate normal. `hasPendingLevelUpChoice` é setado
    // (e salvo) já em OnCombatEnd, assim que um level-up é detectado, ANTES até de
    // CombatResultPanel mostrar qualquer UI; `pendingLevelUpBoxes`/`pendingLevelUpRerollsUsed`
    // são preenchidos logo depois, quando as N caixas são sorteadas (ou resorteadas via "Novo
    // Sorteio") — mesmo padrão de rascunho-persistido-antes-de-aceitar dos campos acima. Limpo
    // só em CombatResultPanel.ApplyBonus, quando o jogador de fato escolhe uma caixa.
    public bool hasPendingLevelUpChoice = false;
    public List<PendingLevelUpBoxRef> pendingLevelUpBoxes = new List<PendingLevelUpBoxRef>();
    public int pendingLevelUpRerollsUsed = 0;

    [Header("Progresso")]
    [Tooltip("N�vel atual do personagem")]
    public int level = 1;

    [Tooltip("Taxa de vit�ria (0 a 100%)")]
    [Range(0f, 100f)] public float winRate = 0f;

    [Tooltip("Experi�ncia atual")]
    public int xpCurrent = 0;

    [Tooltip("Experi�ncia necess�ria para o pr�ximo n�vel")]
    public int xpRequired = 6;

    [Tooltip("Lutas restantes (m�ximo por ciclo)")]
    public int battlesRemaining = 6;

    // Chave estável pro histórico de batalhas por oponente (PlayerPrefs "battles_{id}"/
    // "wins_{id}", ver SelectOpponentController/AttackSequencer) e pro save local
    // (LocalSaveService) — usa `characterId` quando preenchido (personagem com conta/save real,
    // 2026-07-14), senão cai pro nome do próprio asset (Object.name), retrocompatível com todo
    // asset pré-autorado que ainda não tem characterId. Repetir o mesmo PlayerProfile várias
    // vezes no pool de oponentes (ex: 6x Medieval Warrior Girl) soma no mesmo histórico de
    // propósito — é literalmente o mesmo personagem.
    public string OpponentId() => string.IsNullOrEmpty(characterId) ? name : characterId;

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return true;
        return false;
    }

    public SkillData GetSkill(string skillName)
    {
        if (skills == null) return null;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return s;
        return null;
    }

    // Preview-only: mirrors the HP/str/agility/speed/initiative/critChance/critDamageBonus/
    // evasion/reversal/counter/comboChanceBonus/armor bonuses from CombatSimulator.ApplySkillStats /
    // CombatSceneLoader.ApplySkillStats, for display purposes (e.g. MainMenuCharacterPreview)
    // without needing a live PlayerCombat/PlayerState instance. Keep in sync with those two if
    // a skill affecting these stats changes.
    public (int hp, int str, int agility, int speed, int initiative, float criticalChance, float critDamageBonus, float evasion, float reversal, float counter, float comboChanceBonus, float armor, float accuracy, float blockBonus, float reversalAfterBlock, float disarmChanceBonus, float sharpDamageBonus, float heavyDexterityBonus, float heavyHitSpeedBonus) GetEffectiveStats()
    {
        int hp = maxHealth, s = str, a = agility, sp = speed, init = initiative;
        float critChance = criticalChance, critDmgBonus = 0f, eva = evasion, rev = reversal, cnt = counter, combo = 0f, arm = armor, acc = accuracy;
        float blk = blockBonus, revBlk = reversalAfterBlock, disarm = 0f, sharp = 0f, heavyDex = 0f, heavyHitSpd = 0f;

        // Percentuais somados num percentual líquido por status, aplicados uma única vez —
        // evita arredondamento em cascata quando múltiplas skills afetam o mesmo status
        // (ex: Herculean Strength + Immortal no mesmo STR). Ver nota em "Skills que modificam
        // stats" no CLAUDE.md. Iniciativa é flat puro, fora do percentual líquido. evasionPct
        // é multiplicativo sobre eva, aplicado depois de todas as somas flat (Deity zera mesmo
        // que outra skill já tenha somado evasion).
        float hpPct = 0f, sPct = 0f, aPct = 0f, spPct = 0f, evaPct = 0f;

        // Valores de efeito lidos do SkillData equipado (bonusValue1..6) — mesmo mapeamento de
        // CombatSimulator.ApplySkillStats, ver SKILLS_SYSTEM.md.
        // +18 flat já está em profile.maxHealth (aplicado na escolha, CombatResultPanel.ApplyBonus).
        var vitalitySk = GetSkill("Vitality");
        if (vitalitySk != null) hpPct += vitalitySk.bonusValue1;
        // +3 flat já está em profile.str (aplicado na escolha, CombatResultPanel.ApplyBonus);
        // aqui só o percentual, sem penalidade de agilidade.
        var herculeanSk = GetSkill("Herculean Strength");
        if (herculeanSk != null) sPct += herculeanSk.bonusValue1;
        var felineSk = GetSkill("Feline Agility");
        if (felineSk != null) aPct += felineSk.bonusValue1;
        var lightningSk = GetSkill("Lightning Bolt");
        if (lightningSk != null) spPct += lightningSk.bonusValue1;
        // +5 flat já está em profile.speed (aplicado na escolha); initiative/critDmgBonus são flat puro.
        var reconnaissanceSk = GetSkill("Reconnaissance");
        if (reconnaissanceSk != null) { spPct += reconnaissanceSk.bonusValue1; init -= Mathf.RoundToInt(reconnaissanceSk.bonusValue3); critDmgBonus += reconnaissanceSk.bonusValue4; }
        var firstStrikeSk = GetSkill("First Strike");
        if (firstStrikeSk != null) init += Mathf.RoundToInt(firstStrikeSk.bonusValue1);
        var monkSk = GetSkill("Monk");
        if (monkSk != null) { init -= Mathf.RoundToInt(monkSk.bonusValue2); cnt += monkSk.bonusValue1; }
        var counterAttackSk = GetSkill("Counter Attack");
        if (counterAttackSk != null) { blk += counterAttackSk.bonusValue1; revBlk += counterAttackSk.bonusValue2; }
        var sixthSenseSk = GetSkill("Sixth Sense");
        if (sixthSenseSk != null) cnt += sixthSenseSk.bonusValue1;
        var hostilitySk = GetSkill("Hostility");
        if (hostilitySk != null) rev += hostilitySk.bonusValue1;
        var relentlessSk = GetSkill("Relentless");
        if (relentlessSk != null) acc += relentlessSk.bonusValue1;
        var fistsOfFurySk = GetSkill("Fists of Fury");
        if (fistsOfFurySk != null) combo += fistsOfFurySk.bonusValue1;
        // Chaining T2/T3: comboChanceBonus adicional (bonusValue3, novo — T1 fica 0).
        var chainingSk = GetSkill("Chaining");
        if (chainingSk != null) combo += chainingSk.bonusValue3;
        var shockSk = GetSkill("Shock");
        if (shockSk != null) disarm += shockSk.bonusValue1;
        var weaponMasterSk = GetSkill("Weapon Master");
        if (weaponMasterSk != null) sharp += weaponMasterSk.bonusValue1 - 1f; // bonusValue1 é o multiplicador (1.5); sharp é exibido como bônus (+0.5)
        // Informativo apenas: o bônus real só vale enquanto empunha arma Heavy (checado vivo
        // em DodgeChance/CombatPlayer) — aqui só confirma a magnitude da skill, igual ao padrão
        // de Disarm/Sharp acima.
        var bodybuilderSk = GetSkill("Bodybuilder");
        if (bodybuilderSk != null) { heavyDex += bodybuilderSk.bonusValue1; heavyHitSpd += bodybuilderSk.bonusValue2; }
        var armourSk = GetSkill("Armour");
        if (armourSk != null) { arm += armourSk.bonusValue1; spPct -= armourSk.bonusValue2; }
        var toughenedSkinSk = GetSkill("Toughened Skin");
        if (toughenedSkinSk != null) arm += toughenedSkinSk.bonusValue1;
        // Shield: a penalidade de dano causado (bonusValue2, era "armor +=" antes) só existe no
        // simulador (CombatSimulator.CalcDamage) — não é um dos stats desta tupla, então não
        // entra aqui.
        var shieldSk = GetSkill("Shield");
        if (shieldSk != null) blk += shieldSk.bonusValue1;
        // Lead Skeleton (redefinida — antes só dava -15% dano de Heavy, sem entrar aqui):
        // armor/evasion (bonusValue1/2). O dano de arma blunt (Heavy) continua existindo
        // (ver CombatSimulator/PlayerCombat, bonusValue3), só não aparece aqui por não ser
        // um % de stat.
        var leadSkeletonSk = GetSkill("Lead Skeleton");
        if (leadSkeletonSk != null) { arm += leadSkeletonSk.bonusValue1; eva -= leadSkeletonSk.bonusValue2; }
        var immortalSk = GetSkill("Immortal");
        if (immortalSk != null)
        {
            hpPct += immortalSk.bonusValue1;
            sPct  -= immortalSk.bonusValue2;
            aPct  -= immortalSk.bonusValue2;
            spPct -= immortalSk.bonusValue2;
        }
        var deitySk = GetSkill("Deity");
        if (deitySk != null)
        {
            // reversal e iniciativa mantêm o valor hardcoded (7º/8º valor de Deity, excedente
            // aos 6 bonusValue — ver CombatSimulator.ApplySkillStats).
            hpPct  += deitySk.bonusValue1;
            sPct   += deitySk.bonusValue2;
            aPct   -= deitySk.bonusValue3;
            spPct  -= deitySk.bonusValue4;
            evaPct -= deitySk.bonusValue5;
            rev    += 0.40f;
            init   -= Mathf.RoundToInt(deitySk.bonusValue6);
        }
        var untouchableSk = GetSkill("Untouchable");
        if (untouchableSk != null) eva += untouchableSk.bonusValue1;
        var balletShoesSk = GetSkill("Ballet Shoes");
        if (balletShoesSk != null) eva += balletShoesSk.bonusValue1;

        if (hpPct != 0f || sPct != 0f || aPct != 0f || spPct != 0f)
        {
            hp = Mathf.RoundToInt(hp * (1f + hpPct));
            s  = Mathf.RoundToInt(s * (1f + sPct));
            a  = Mathf.RoundToInt(a * (1f + aPct));
            sp = Mathf.RoundToInt(sp * (1f + spPct));
        }
        // Incondicional (não só quando evaPct != 0) pra também garantir o floor em 0 quando só
        // Lead Skeleton (-15% flat) deixa o total negativo.
        eva = Mathf.Max(0f, eva * (1f + evaPct));

        return (hp, s, a, sp, init, critChance, critDmgBonus, eva, rev, cnt, combo, arm, acc, blk, revBlk, disarm, sharp, heavyDex, heavyHitSpd);
    }

    // Valores de damage dos assets representativos de cada arquétipo, pra preview de UI sem
    // depender de carregar o WeaponData em runtime — Satyr1 (Adaga: Sharp+Fast), trio Sword
    // (Espada: Sharp), Golem3 (Pesado: Heavy+Blunt). Cada WeaponData agora tem um campo
    // `damage` fixo próprio (sem mais range aleatório por categoria) — se esses 3 assets
    // mudarem de damage, atualizar aqui também (mesmo padrão de "cópia independente pra
    // exibição" das outras faixas de preview deste arquivo).
    private const int DaggerArchetypeDamage = 10;
    private const int SwordArchetypeDamage  = 14;
    private const int HeavyArchetypeDamage  = 40;

    // Faixas de dano normal (sem crítico) por arquétipo de arma, com STR efetivo
    // (GetEffectiveStats) e bônus de skill já aplicados (Martial Arts dobra o desarmado;
    // Weapon Master dá +50% em armas Sharp) — espelha WeaponBaseDamage()/CalcDamage() de
    // CombatSimulator/PlayerCombat para fins de exibição (CharacterPanel, aba Stats), sem
    // precisar de uma instância de combate em runtime. STR soma flat (não multiplica) — ver
    // Fórmula de Dano no CLAUDE.md. Sem mais range aleatório: min==max em todo arquétipo
    // (collapsa pra um número só nos helpers SetStatRange/SetStatRangeWithBase do CharacterPanel).
    public (int unarmedMin, int unarmedMax, int daggerMin, int daggerMax, int swordMin, int swordMax, int heavyMin, int heavyMax) GetWeaponDamageRanges()
    {
        int   str = GetEffectiveStats().str;
        var martialArtsSk = GetSkill("Martial Arts");
        int unarmedBase = martialArtsSk != null ? Mathf.RoundToInt(UnarmedStats.Damage * (martialArtsSk.bonusValue1 > 0f ? martialArtsSk.bonusValue1 : 2f)) : UnarmedStats.Damage;
        int u  = Mathf.Max(1, unarmedBase + str);
        var weaponMasterSk = GetSkill("Weapon Master");
        float sharpMult = weaponMasterSk != null ? (weaponMasterSk.bonusValue1 > 0f ? weaponMasterSk.bonusValue1 : 1.5f) : 1f;
        int d  = Mathf.Max(1, Mathf.RoundToInt((DaggerArchetypeDamage + str) * sharpMult));
        int sw = Mathf.Max(1, Mathf.RoundToInt((SwordArchetypeDamage  + str) * sharpMult));
        int h  = Mathf.Max(1, HeavyArchetypeDamage + str);
        return (u, u, d, d, sw, sw, h, h);
    }

    // Faixa "base" (sem Weapon Master) de Adaga/Espada, pra UI mostrar base→efetivo (mesmo
    // padrão de Desarmado/Martial Arts). Não inclui no tuple principal pra não duplicar os
    // campos unarmed/heavy, que nenhuma skill de "sharp" afeta.
    public (int daggerMin, int daggerMax, int swordMin, int swordMax) GetBaseSharpDamageRanges()
    {
        int str = GetEffectiveStats().str;
        int d  = Mathf.Max(1, DaggerArchetypeDamage + str);
        int sw = Mathf.Max(1, SwordArchetypeDamage  + str);
        return (d, d, sw, sw);
    }
}
