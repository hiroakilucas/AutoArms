using System.Collections.Generic;

// Snapshot dos stats/loadout/skills/pets de UM personagem no momento exato de uma luta —
// separado de CharacterDTO porque este não carrega progressão (winRate/xpCurrent/
// battlesRemaining/isFavorite/accountScope não fazem sentido "congelados" numa luta passada;
// além de nunca precisarem ser lidos de volta pra um PlayerProfile de verdade, só exibidos).
// Reaproveita WeaponTierRef/SkillTierRef/PetTierRef (CharacterDTO.cs) — mesma representação
// nome+tier já usada pra armas/skills/pets em todo o resto do save.
[System.Serializable]
public class ReplayPlayerSnapshotDTO
{
    public string profileName;
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
}

// Documento salvo em users/{uid}/characters/{characterId}/replays/{replayId} (Opção B aprovada —
// log de eventos completo, não seed+snapshot só — ver análise/decisão registrada em
// ARQUITETURA.md). p1Snapshot é sempre o DONO deste replay (o characterId do caminho), p2Snapshot
// é sempre o adversário — `result` é sempre do ponto de vista de p1.
[System.Serializable]
public class ReplayDTO
{
    // DateTime.UtcNow.Ticks no momento em que a luta terminou — usado tanto pra ordenar
    // (orderBy(createdAtTicks, desc)) quanto pra rotação client-side (ReplayRecorder/FirestoreService).
    public long createdAtTicks;

    public string opponentCharacterId;
    public string opponentName;

    // "win" | "loss" — do ponto de vista de p1Snapshot (o dono deste replay).
    public string result;

    // Seed real usado em CombatSimulator.Simulate() nesta luta (CombatSceneLoader gera e passa
    // explicitamente, em vez de deixar o simulador cair no default aleatório) — não é
    // estritamente necessário pro replay funcionar (os eventos já vêm todos resolvidos abaixo),
    // mas é praticamente grátis guardar e serve de auditoria/debug futuro (ex: reproduzir a luta
    // de novo com as REGRAS atuais pra comparar contra o resultado gravado, e investigar até que
    // ponto um rebalance específico mudaria o desfecho).
    public int seed;

    // Contagem de eventos — só pra listagem rápida (ex: indicador de "luta longa/curta") sem
    // precisar baixar o documento inteiro primeiro.
    public int eventCount;

    // Nº de rounds REAIS da luta (CombatSimulator.RoundCount, 2026-07-18) — não um valor
    // estimado a partir de eventCount (pedido explícito do usuário pro popup de REPLAYS). 0 em
    // replays salvos ANTES desta mudança (campo ausente no documento) — CharacterPanel cai de
    // volta pra `eventCount` nesse caso, rotulado como "eventos", não "rounds", pra nunca exibir
    // um número fabricado como se fosse round de verdade.
    public int roundCount;

    public ReplayPlayerSnapshotDTO p1Snapshot;
    public ReplayPlayerSnapshotDTO p2Snapshot;

    public List<ReplayEventDTO> events = new List<ReplayEventDTO>();
}
