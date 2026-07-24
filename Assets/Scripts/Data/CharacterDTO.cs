using System;
using System.Collections.Generic;

// Par nome+tier de uma arma equipada - representa um WeaponData sem guardar a referencia de
// asset em si (ScriptableObjects nao podem ser serializados em JSON/Firestore diretamente).
// `name` e o nome "de familia" (sem sufixo " T1/T2/T3", ver WeaponNameUtil.StripWeaponTierSuffix)
// - resolvido de volta pro WeaponData real via WeaponDatabase.FindByFamilyNameAndTier.
[Serializable]
public class WeaponTierRef
{
    public string name;
    public int tier;
}

// Mesma ideia de WeaponTierRef, mas pra SkillData - SkillData.skillName ja e identico em todos
// os tiers (sem sufixo), resolvido via SkillDatabase.FindByFamilyNameAndTier.
[Serializable]
public class SkillTierRef
{
    public string name;
    public int tier;
}

// Mesma ideia de WeaponTierRef/SkillTierRef, mas pra PetData (2026-07-16, tiers de pet) - `type`
// e PetType.ToString() (Mouse/Monkey/Boar), resolvido de volta via
// PetDatabase.FindByTypeAndTier.
[Serializable]
public class PetTierRef
{
    public string type;
    public int tier;
}

// Uma caixa de escolha de level-up de combate ainda não confirmada (2026-07-25, bug real
// corrigido - fechar o app com a tela de escolha aberta perdia XP/level/bônus, sem chance de
// retomar). `kind` = "attribute"/"skill"/"weapon"/"pet"; `attrIndex` só usado quando
// kind=="attribute" (índice 0-9 na tabela fixa de bônus de atributo, ver LevelUpOption.Name());
// `name`+`tier` (mesmo shape de WeaponTierRef/SkillTierRef/PetTierRef) só usados pra skill/arma/
// pet - resolvido de volta pro LevelUpOption real via CombatResultPanel.ResolvePendingBox.
[Serializable]
public class PendingLevelUpBoxRef
{
    public string kind;
    public int attrIndex;
    public string name;
    public int tier;
}

// Representacao serializavel da progressao de UM PlayerProfile - usada hoje pelo save local em
// JSON (LocalSaveService) e pensada pra ser o mesmo formato usado pelo save na nuvem
// (Firestore, ver ARQUITETURA.md e o plano de contas/backend) mais pra frente. Sem nenhuma
// referencia de asset (WeaponData/SkillData/characterPrefab/previewIcon nao cabem em
// JSON/Firestore) - so os campos de PROGRESSAO do personagem, os mesmos que
// CombatSceneLoader ja le de qualquer PlayerProfile pra montar o combate.
[Serializable]
public class CharacterDTO
{
    // Chave de save - profile.characterId, ou profile.name se characterId estiver vazio (ver
    // PlayerProfile.OpponentId(), mesmo criterio).
    public string characterId;

    // ID do "molde" PlayerProfile de origem (2026-07-23, sistema de compra de personagens/case
    // opening - ver ARQUITETURA.md "Modelo de roster multi-personagem"). Igual ao nome do asset
    // Unity (mesma chave que PlayerProfileConverter.FromOpponentIndexMap ja usa pra achar o
    // molde de um oponente) - permite que uma conta possua VARIOS characterId diferentes, cada
    // um instancia de um characterTypeId (raridade/prefab/stats iniciais). Vazio nos documentos
    // gravados antes desta data (personagem "original" da conta, unico que existia ate entao) -
    // retrocompativel, nao precisa de migracao.
    public string characterTypeId;

    // Escopo de conta (2026-07-15, correcao de bug real - LocalSaveService.cs) - uid do Firebase
    // Auth de quem salvou isso, ou "offline" se ninguem estava logado no momento do save. Sem
    // isso, o save.json local era um dicionario global por characterId SEM vinculo de conta -
    // qualquer conta que logasse no mesmo device/executavel herdava o progresso salvo por outra
    // conta anteriormente (bug reportado pelo usuario: conta nova veio com personagens de outra
    // conta). LocalSaveService usa characterId+accountScope juntos como chave, nao characterId
    // sozinho. Entradas com accountScope vazio (gravadas ANTES desta correcao) sao tratadas como
    // orfas/nao confiaveis e nunca aplicadas a nenhuma conta - ver LocalSaveService.EnsureLoaded.
    public string accountScope;

    public string profileName;
    public int level;
    public float winRate;
    public int xpCurrent;
    public int xpRequired;
    public int battlesRemaining;
    public bool isFavorite;
    public int rarity; // (int)CharacterRarity

    // Ver PlayerProfile.caseUnlocksResolved - marca que a sequencia de unlocks de skill/arma/pet
    // do case opening (CharacterUnlockEngine) ja rodou por completo pra este characterId.
    public bool caseUnlocksResolved;

    // Ver PlayerProfile.caseUnlocksAcceptedCount - ponto de retomada se a sequencia for
    // interrompida (app fechado) antes de caseUnlocksResolved virar true.
    public int caseUnlocksAcceptedCount;

    // Ver PlayerProfile.pendingUnlockIndex/Kind/Name/Tier/RerollsUsed - rascunho do unlock atual
    // (ainda nao aceito), persistido pra sobreviver a fechar o app no meio de um reveal.
    public int pendingUnlockIndex;
    public string pendingUnlockKind;
    public string pendingUnlockName;
    public int pendingUnlockTier;
    public int pendingUnlockRerollsUsed;

    // Ver PlayerProfile.hasPendingLevelUpChoice/pendingLevelUpBoxes/pendingLevelUpRerollsUsed -
    // escolha de level-up de COMBATE ainda não confirmada (sistema diferente do unlock do case
    // opening acima - este é o "Continuar"/pirâmide de caixas do AttackSequencer.OnCombatEnd).
    public bool hasPendingLevelUpChoice;
    public List<PendingLevelUpBoxRef> pendingLevelUpBoxes = new List<PendingLevelUpBoxRef>();
    public int pendingLevelUpRerollsUsed;

    public int maxHealth;
    public int str;
    public int agility;
    public int speed;
    public float armor;
    public float evasion;
    public float accuracy;
    public int initiative;
    public float reversal;
    public float counter;
    public float blockBonus;
    public float reversalAfterBlock;
    public float criticalChance;
    public float hitSpeed;

    public List<WeaponTierRef> weapons = new List<WeaponTierRef>();
    public List<SkillTierRef> skills = new List<SkillTierRef>();
    public List<PetTierRef> pets = new List<PetTierRef>();

    // DateTime.UtcNow.Ticks no momento do save - usado pra reconciliacao "ultimo gravado ganha"
    // quando o save na nuvem (Firestore) entrar (ver ARQUITETURA.md/plano de contas). Sem uso
    // ainda enquanto so existe o save local.
    public long updatedAtTicks;
}
