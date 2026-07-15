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
    public List<string> pets = new List<string>(); // PetType.ToString()

    // DateTime.UtcNow.Ticks no momento do save - usado pra reconciliacao "ultimo gravado ganha"
    // quando o save na nuvem (Firestore) entrar (ver ARQUITETURA.md/plano de contas). Sem uso
    // ainda enquanto so existe o save local.
    public long updatedAtTicks;
}
