using System;
using System.Collections.Generic;
using UnityEngine;

// Conversao entre PlayerProfile (ScriptableObject, autorado no projeto) e CharacterDTO
// (serializavel, sem referencia de asset) - extraido de LocalSaveService (2026-07-15, Fatia 3)
// pra ser compartilhado entre o save local em JSON e o save na nuvem no Firestore, sem duplicar
// esta logica em dois lugares (evita as duas copias divergirem conforme PlayerProfile ganha
// campos novos no futuro).
public static class PlayerProfileConverter
{
    private static WeaponDatabase _weaponDatabase;
    private static SkillDatabase _skillDatabase;
    private static PetDatabase _petDatabase;

    // Snapshot do estado de progressão de cada PlayerProfile na PRIMEIRA vez que ele é tocado
    // nesta sessão do processo (2026-07-15, correção de bug real de isolamento entre contas) —
    // os 72 `PlayerProfile` são assets ScriptableObject que vivem em memória durante todo o
    // processo, não só durante uma cena; sem isso, sair da conta A (que aplicou seu progresso
    // num profile) e entrar na conta B no MESMO processo (sem fechar o jogo) fazia B herdar o
    // estado em memória que A deixou, mesmo com o save.json já isolado por conta (ver
    // LocalSaveService). Capturado ANTES de qualquer sync aplicar dado de conta nenhuma
    // (CloudSyncService.SyncCharacterAsync chama isto como primeira linha) — representa o estado
    // "de fábrica" do asset (o que o Inspector definiu), não o de nenhuma conta específica.
    // RestoreAllPristine() devolve todo profile já tocado a esse estado de fábrica, chamado no
    // logout (MainMenuController.OnLogoutClicked) antes de outra conta poder logar.
    private static readonly Dictionary<PlayerProfile, CharacterDTO> _pristineSnapshots = new Dictionary<PlayerProfile, CharacterDTO>();

    public static void CapturePristineIfNeeded(PlayerProfile profile)
    {
        if (profile == null || _pristineSnapshots.ContainsKey(profile)) return;
        _pristineSnapshots[profile] = ToDTO(profile);
    }

    public static void RestoreAllPristine()
    {
        foreach (var kvp in _pristineSnapshots)
        {
            if (kvp.Key == null) continue;
            ApplyDTO(kvp.Key, kvp.Value);
            _ownerScope.Remove(kvp.Key);
        }
    }

    // "Dono" atual do estado em memória de cada PlayerProfile já tocado nesta sessão (2026-07-15,
    // 2ª rodada da correção de isolamento entre contas — bug persistiu mesmo com
    // CapturePristineIfNeeded/RestoreAllPristine: usuário reportou conta nova recebendo um
    // personagem em Level 2, nem o estado de fábrica nem o último nível jogado). O ponto frágil da
    // 1ª correção era `CloudSyncService.SyncCharacterAsync` gravar (`LocalSaveService.Save`) DE
    // FORMA INCONDICIONAL ao final — ou seja, era um modelo "opt-out": assumia que era seguro
    // gravar o que quer que estivesse em memória, e só o reset no logout (`RestoreAllPristine`)
    // impedia vazamento. Se esse reset falhar por QUALQUER motivo (troca de cena descarregando e
    // recarregando o asset, ordem de chamadas de uma sessão anterior, etc. — não foi possível
    // confirmar a causa exata por análise estática), nada mais barra a gravação errada.
    //
    // Este dicionário inverte pra um modelo "opt-in": só é seguro gravar o profile sob a conta
    // atual se pudermos AFIRMAR que o estado em memória pertence a ela (ou nunca pertenceu a
    // ninguém ainda). `null` = nunca "reivindicado" por nenhuma conta desde o último reset —
    // seguro pra qualquer conta reivindicar (personagem realmente novo pra essa conta, ou acabou
    // de passar por `RestoreAllPristine`). Ver uso em `LocalSaveService.Save`/`ApplyIfSaved` e
    // `CloudSyncService.SyncCharacterAsync`.
    private static readonly Dictionary<PlayerProfile, string> _ownerScope = new Dictionary<PlayerProfile, string>();

    public static void MarkOwnerScope(PlayerProfile profile, string scope)
    {
        if (profile == null) return;
        _ownerScope[profile] = scope;
    }

    public static string GetOwnerScope(PlayerProfile profile)
    {
        if (profile == null) return null;
        return _ownerScope.TryGetValue(profile, out var scope) ? scope : null;
    }

    // WeaponDatabase/SkillDatabase precisaram ser movidos pra Assets/Resources/ (2026-07-14,
    // mesmo padrao ja usado por SelectedProfileHolder/SelectedOpponentHolder/BattleGround) pra
    // este conversor conseguir resolve-los em runtime sem depender de nenhuma cena especifica
    // ter uma referencia [SerializeField] pra eles.
    private static WeaponDatabase GetWeaponDatabase()
    {
        if (_weaponDatabase == null) _weaponDatabase = Resources.Load<WeaponDatabase>("WeaponDatabase");
        return _weaponDatabase;
    }

    private static SkillDatabase GetSkillDatabase()
    {
        if (_skillDatabase == null) _skillDatabase = Resources.Load<SkillDatabase>("SkillDatabase");
        return _skillDatabase;
    }

    private static PetDatabase GetPetDatabase()
    {
        if (_petDatabase == null) _petDatabase = Resources.Load<PetDatabase>("PetDatabase");
        return _petDatabase;
    }

    public static CharacterDTO ToDTO(PlayerProfile profile)
    {
        var dto = new CharacterDTO
        {
            characterId = profile.OpponentId(),
            profileName = profile.profileName,
            level = profile.level,
            winRate = profile.winRate,
            xpCurrent = profile.xpCurrent,
            xpRequired = profile.xpRequired,
            battlesRemaining = profile.battlesRemaining,
            isFavorite = profile.isFavorite,
            rarity = (int)profile.rarity,
            maxHealth = profile.maxHealth,
            str = profile.str,
            agility = profile.agility,
            speed = profile.speed,
            armor = profile.armor,
            evasion = profile.evasion,
            accuracy = profile.accuracy,
            initiative = profile.initiative,
            reversal = profile.reversal,
            counter = profile.counter,
            blockBonus = profile.blockBonus,
            reversalAfterBlock = profile.reversalAfterBlock,
            criticalChance = profile.criticalChance,
            hitSpeed = profile.hitSpeed,
            updatedAtTicks = DateTime.UtcNow.Ticks,
        };

        if (profile.weapons != null)
            foreach (var w in profile.weapons)
                if (w != null)
                    dto.weapons.Add(new WeaponTierRef { name = WeaponNameUtil.StripWeaponTierSuffix(w.weaponName), tier = w.tier });

        if (profile.skills != null)
            foreach (var s in profile.skills)
                if (s != null)
                    dto.skills.Add(new SkillTierRef { name = s.skillName, tier = s.tier });

        if (profile.pets != null)
            foreach (var p in profile.pets)
                if (p != null)
                    dto.pets.Add(new PetTierRef { type = p.petType.ToString(), tier = p.tier });

        return dto;
    }

    public static void ApplyDTO(PlayerProfile profile, CharacterDTO dto)
    {
        profile.level = dto.level;
        profile.winRate = dto.winRate;
        profile.xpCurrent = dto.xpCurrent;
        profile.xpRequired = dto.xpRequired;
        profile.battlesRemaining = dto.battlesRemaining;
        profile.isFavorite = dto.isFavorite;
        profile.rarity = (CharacterRarity)dto.rarity;
        profile.maxHealth = dto.maxHealth;
        profile.str = dto.str;
        profile.agility = dto.agility;
        profile.speed = dto.speed;
        profile.armor = dto.armor;
        profile.evasion = dto.evasion;
        profile.accuracy = dto.accuracy;
        profile.initiative = dto.initiative;
        profile.reversal = dto.reversal;
        profile.counter = dto.counter;
        profile.blockBonus = dto.blockBonus;
        profile.reversalAfterBlock = dto.reversalAfterBlock;
        profile.criticalChance = dto.criticalChance;
        profile.hitSpeed = dto.hitSpeed;

        var weaponDb = GetWeaponDatabase();
        if (weaponDb != null && dto.weapons != null)
        {
            var weapons = new List<WeaponData>();
            foreach (var w in dto.weapons)
            {
                var data = weaponDb.FindByFamilyNameAndTier(w.name, w.tier);
                if (data != null) weapons.Add(data);
            }
            profile.weapons = weapons;
        }

        var skillDb = GetSkillDatabase();
        if (skillDb != null && dto.skills != null)
        {
            var skills = new List<SkillData>();
            foreach (var s in dto.skills)
            {
                var data = skillDb.FindByFamilyNameAndTier(s.name, s.tier);
                if (data != null) skills.Add(data);
            }
            profile.skills = skills;
        }

        var petDb = GetPetDatabase();
        if (petDb != null && dto.pets != null)
        {
            var pets = new List<PetData>();
            foreach (var p in dto.pets)
            {
                if (!Enum.TryParse(p.type, out PetType pt)) continue;
                var data = petDb.FindByTypeAndTier(pt, p.tier);
                if (data != null) pets.Add(data);
            }
            profile.pets = pets;
        }
    }

    // Reconstrói um PlayerProfile RUNTIME (não um asset do projeto) a partir de um documento de
    // opponents_index (Fatia 6, 2026-07-15) — usado pra transformar um adversário achado na busca
    // online numa instância que o resto do jogo (CombatSceneLoader, etc.) já sabe processar sem
    // nenhuma mudança, igual já acontece com qualquer PlayerProfile normal.
    //
    // `dto.characterId` hoje é sempre o nome de um PlayerProfile pré-autorado do projeto (não
    // existe ainda personagem "criado do zero" por conta, ver decisão da Fatia 4) — usado aqui
    // pra achar o "molde" certo em `templateCatalog` e copiar dele só os campos VISUAIS
    // (characterPrefab/previewIcon/splashArt/attackSettings/scale/startPos), que não fazem parte
    // do documento do Firestore. Retorna null se não achar nenhum molde com esse nome (documento
    // órfão/corrompido, ou o personagem foi removido do projeto).
    public static PlayerProfile FromOpponentIndexMap(Dictionary<string, object> map, CharacterDatabase templateCatalog)
    {
        var dto = CharacterDTOMap.FromMap(map);
        if (string.IsNullOrEmpty(dto.characterId) || templateCatalog?.unlockedCharacters == null) return null;

        PlayerProfile template = null;
        foreach (var t in templateCatalog.unlockedCharacters)
        {
            if (t != null && t.name == dto.characterId) { template = t; break; }
        }
        if (template == null) return null;

        var runtime = ScriptableObject.CreateInstance<PlayerProfile>();
        runtime.characterId = dto.characterId; // OpponentId() precisa disso — .name de uma instância recém-criada não bate com o personagem de verdade
        // ApplyDTO (chamado abaixo) nunca escreve profileName — num profile JÁ EXISTENTE esse
        // campo nunca muda, então nunca precisou reaplicar; numa instância nova como esta, fica
        // vazio sem essa linha.
        runtime.profileName = template.profileName;
        runtime.characterPrefab = template.characterPrefab;
        runtime.previewIcon = template.previewIcon;
        runtime.splashArt = template.splashArt;
        runtime.attackSettings = template.attackSettings;
        runtime.scale = template.scale;
        runtime.startPos = template.startPos;
        // Adversário achado online nunca deve aparecer no grid/troca rápida do PRÓPRIO jogador —
        // só existe pra virar SelectedOpponentHolder.currentOpponentProfile.
        runtime.isUnlockedForSelection = false;
        runtime.isPlayable = false;

        ApplyDTO(runtime, dto);
        return runtime;
    }
}
