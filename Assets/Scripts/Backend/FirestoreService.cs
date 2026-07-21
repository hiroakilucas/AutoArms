using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Wrapper fino sobre Firebase.Firestore (Fatia 3, 2026-07-15) - persistencia de personagem na
// nuvem, ainda escopado a "1 conta/1 personagem" (o ID usado hoje é PlayerProfile.OpponentId(),
// mesmo criterio de sempre - virar uma lista de vários personagens por conta é a Fatia 4).
// Documento em users/{uid}/characters/{characterId}, formato CharacterDTO via CharacterDTOMap
// (Dictionary<string,object> manual - ver comentário em CharacterDTOMap.cs pro motivo de não
// usar os atributos [FirestoreData]/[FirestoreProperty] do SDK).
public static class FirestoreService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;
    private static bool _persistenceConfigured;

    // PersistenceEnabled precisa ser setado antes do primeiro uso real do Firestore - guard
    // evita reconfigurar em toda chamada. Cache offline nativo do SDK (distinto do
    // LocalSaveService, que é nosso próprio JSON) - cobre "já logado, rede caiu no meio da
    // sessão"; ver Estratégia offline no plano de contas.
    //
    // Bug real (2026-07-18): se QUALQUER outro código tocar `FirebaseFirestore.DefaultInstance`
    // antes desta função rodar pela 1ª vez (ex: `OpponentSearchService.FetchOpponentsAsync`, que
    // usa a instância direto sem passar por `FirestoreService`; ou o jogador entrando em Play Mode
    // direto numa cena que não passa por `00_Login`/`LoginController.SyncCharacterRoutine`), o SDK
    // já considera a instância "em uso" e `Db.Settings.PersistenceEnabled = true` lança
    // `InvalidOperationException` ("cannot be modified after calling non-static methods...") — sem
    // o try/catch abaixo, isso derrubava TODA gravação/leitura da sessão inteira (personagem,
    // matchHistory, opponents_index, replay), não só a chamada que disparou o erro, porque
    // `_persistenceConfigured` nunca chegava a ser marcado e a exceção subia pro chamador. Marcado
    // ANTES do try (não depois) — falhar não deve fazer a próxima chamada tentar de novo; o cache
    // offline simplesmente não liga nessa sessão, mas leitura/escrita seguem funcionando
    // normalmente sem ele (é só uma otimização de cache, não um requisito funcional). `Public` (via
    // wrapper, ver TryEnsurePersistence) pra código fora desta classe (`OpponentSearchService`)
    // poder chamar isto ANTES do próprio acesso direto ao Firestore, na ordem certa.
    private static void EnsurePersistence()
    {
        if (_persistenceConfigured) return;
        _persistenceConfigured = true;
        try
        {
            Db.Settings.PersistenceEnabled = true;
        }
        catch (Exception)
        {
            // Silencioso de propósito (ver Logging Policy no CLAUDE.md — não é uma falha real,
            // o Firestore continua funcionando sem o cache offline).
        }
    }

    // Wrapper público (2026-07-18) — permite qualquer código que acesse o Firestore por fora desta
    // classe (ex: OpponentSearchService) chamar a MESMA configuração de persistência antes do
    // próprio uso, em vez de descobrir o bug acima por conta própria.
    public static void TryEnsurePersistence() => EnsurePersistence();

    private static DocumentReference CharacterDoc(string uid, string characterId) =>
        Db.Collection("users").Document(uid).Collection("characters").Document(characterId);

    public static async Task<(bool success, string error)> SaveCharacterAsync(string uid, CharacterDTO dto)
    {
        try
        {
            EnsurePersistence();
            // SetOptions.MergeAll (2026-07-20, bug real corrigido — era SetAsync sem merge, ou
            // seja, SOBRESCREVIA O DOCUMENTO INTEIRO com só os campos do CharacterDTO). Isso
            // apagava silenciosamente qualquer campo gravado por outro sistema que não passa por
            // este DTO — energyCurrent/lastEnergyTimestamp (EnergyService.cs) são o caso real: a
            // cada luta, este save (disparado pelo XP ganho) resetava a energia pra "documento
            // sem os campos ainda", fazendo EnergyService reinicializar pra cheio e consumir 1 de
            // novo, sempre no mesmo número (reportado pelo usuário: "energia sempre volta pra 9").
            // Com merge, todo campo do CharacterDTOMap continua sendo sobrescrito normalmente
            // (todos estão presentes no payload a cada chamada — level/weapons/skills/etc não
            // ficam "presos" no valor antigo), só os campos de FORA do DTO (energia) deixam de
            // ser apagados.
            await CharacterDoc(uid, dto.characterId).SetAsync(CharacterDTOMap.ToMap(dto), SetOptions.MergeAll);
            return (true, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao salvar personagem '{dto.characterId}': {e.Message}");
            return (false, e.Message);
        }
    }

    // Retorna null se o documento não existir (personagem ainda não sincronizado nessa conta) ou
    // se a leitura falhar (erro logado, tratado como "sem dado na nuvem" pelo chamador).
    public static async Task<CharacterDTO> LoadCharacterAsync(string uid, string characterId)
    {
        try
        {
            EnsurePersistence();
            DocumentSnapshot snap = await CharacterDoc(uid, characterId).GetSnapshotAsync();
            if (!snap.Exists) return null;
            return CharacterDTOMap.FromMap(snap.ToDictionary());
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao carregar personagem '{characterId}': {e.Message}");
            return null;
        }
    }

    // opponents_index (Fatia 5, 2026-07-15) — coleção FLAT no topo do banco (não dentro de
    // users/{uid}), superfície pública de busca de adversário (lida por OpponentSearchService,
    // Fatia 6). ID do documento é "{ownerUid}_{characterId}", NÃO só characterId — characterId hoje é só o
    // nome do personagem (PlayerProfile.OpponentId()), que não é único entre contas diferentes
    // (duas contas jogando de "Medieval Warrior", por exemplo, colidiriam no mesmo documento e
    // uma sobrescreveria o índice da outra se o ID fosse só characterId). ownerUid/characterId
    // continuam gravados como CAMPOS também, pra consulta/exibição.
    private static DocumentReference OpponentIndexDoc(string ownerUid, string characterId) =>
        Db.Collection("opponents_index").Document($"{ownerUid}_{characterId}");

    // Chamado sempre junto de SaveCharacterAsync, no mesmo instante (ver LocalSaveService.Save) —
    // regra registrada em ARQUITETURA.md ("Stat base vs. stat efetivo"): os campos eff* aqui são
    // GetEffectiveStats() (base + bônus percentual de skills como Lightning Bolt/Herculean
    // Strength), calculados a partir do MESMO profile/momento que gera o CharacterDTO base salvo
    // em users/{uid}/characters — evita os dois documentos ficarem dessincronizados entre si.
    //
    // Correção (Fatia 6, 2026-07-15): a versão original desta função só gravava os campos eff*
    // (pensados só pra EXIBIÇÃO). Pra de fato LUTAR contra um adversário achado, o
    // CombatSimulator precisa dos stats BASE + lista de skills/armas (as skills têm mecânicas de
    // combate que um número efetivo único não cobre - contra-ataque, bônus condicional de arma
    // Sharp, etc.) - sem isso, reconstruir o oponente pra combate duplicaria o bônus percentual
    // da skill (mesmo bug já documentado em "Stat base vs. stat efetivo", mas na direção
    // contrária). Reaproveita CharacterDTOMap.ToMap(dto) (mesmos campos base/weapons/skills que
    // já vão pra users/{uid}/characters) e só ACRESCENTA os campos extras específicos de
    // opponents_index (ownerUid, levelBucket, eff*, randomSeed) por cima.
    public static async Task<(bool success, string error)> SaveOpponentIndexAsync(string ownerUid, PlayerProfile profile, CharacterDTO dto)
    {
        try
        {
            EnsurePersistence();

            var (effHp, effStr, effAgi, effSpd, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) = profile.GetEffectiveStats();

            var map = CharacterDTOMap.ToMap(dto);
            map["ownerUid"] = ownerUid;
            // Bucket simples (grupos de 5 níveis) pra range query na busca de adversário.
            map["levelBucket"] = (dto.level / 5) * 5;
            map["effHp"] = effHp;
            map["effStr"] = effStr;
            map["effAgility"] = effAgi;
            map["effSpeed"] = effSpd;
            map["randomSeed"] = UnityEngine.Random.value;

            await OpponentIndexDoc(ownerUid, dto.characterId).SetAsync(map);
            return (true, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao salvar opponents_index '{dto.characterId}': {e.Message}");
            return (false, e.Message);
        }
    }

    private static DocumentReference MatchHistoryDoc(string uid, string opponentCharacterId) =>
        Db.Collection("users").Document(uid).Collection("matchHistory").Document(opponentCharacterId);

    // Espelho no Firestore do histórico de batalhas que já existia só em PlayerPrefs (Fatia 6,
    // 2026-07-15) — AttackSequencer.OnCombatEnd continua gravando o PlayerPrefs local sem
    // mudança nenhuma (leitura do card em SelectOpponentController continua PlayerPrefs-only por
    // enquanto, decisão de escopo desta fatia); isto aqui só garante que o dado também exista na
    // nuvem pra uso futuro (histórico entre dispositivos, ranking, etc.), sem exigir uma leitura
    // extra por card agora. `battles`/`wins` são o total ACUMULADO (não incremento) — o chamador
    // já soma em cima do valor lido do PlayerPrefs antes de chamar isto.
    public static async Task<(bool success, string error)> SaveMatchHistoryAsync(string uid, string opponentCharacterId, int battles, int wins)
    {
        try
        {
            EnsurePersistence();
            var map = new Dictionary<string, object>
            {
                { "battles", battles },
                { "wins", wins },
                { "lastPlayedAtTicks", DateTime.UtcNow.Ticks },
            };
            await MatchHistoryDoc(uid, opponentCharacterId).SetAsync(map);
            return (true, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao salvar matchHistory '{opponentCharacterId}': {e.Message}");
            return (false, e.Message);
        }
    }

    // Replays (log de eventos completo, Opção B — ver ARQUITETURA.md/CHANGELOG.md) — subcoleção
    // por personagem, mesmo padrão de matchHistory acima, mas com um documento por LUTA em vez de
    // um contador acumulado por adversário.
    public const int MaxReplaysPerCharacter = 10;

    private static CollectionReference ReplaysCollection(string uid, string characterId) =>
        Db.Collection("users").Document(uid).Collection("characters").Document(characterId).Collection("replays");

    public static async Task<(bool success, string error)> SaveReplayAsync(string uid, string characterId, ReplayDTO dto)
    {
        try
        {
            EnsurePersistence();
            var collection = ReplaysCollection(uid, characterId);
            await collection.Document().SetAsync(ReplayDTOMap.ToMap(dto));
            await TrimOldReplaysAsync(collection);
            return (true, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao salvar replay de '{characterId}': {e.Message}");
            return (false, e.Message);
        }
    }

    // Lista os últimos replays de um personagem (botão "Replays" no CharacterPanel) — mesmo limit
    // de MaxReplaysPerCharacter, já que nunca deveria haver mais que isso graças à rotação acima
    // (mas não depende disso pra funcionar; só lista o que existir, até o teto).
    public static async Task<List<(string replayId, ReplayDTO dto)>> ListReplaysAsync(string uid, string characterId)
    {
        var result = new List<(string, ReplayDTO)>();
        try
        {
            EnsurePersistence();
            QuerySnapshot snap = await ReplaysCollection(uid, characterId)
                .OrderByDescending("createdAtTicks").Limit(MaxReplaysPerCharacter).GetSnapshotAsync();
            foreach (var doc in snap.Documents)
                result.Add((doc.Id, ReplayDTOMap.FromMap(doc.ToDictionary())));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreService] Falha ao listar replays de '{characterId}': {e.Message}");
        }
        return result;
    }

    // Rotação client-side (v1 — sem Cloud Function no projeto ainda, ver nota em ARQUITETURA.md
    // "Replay fabricado client-side"): o próprio cliente que acabou de gravar o replay novo lê os
    // mais recentes por createdAtTicks e apaga o que sobrar além de MaxReplaysPerCharacter. Limit
    // bem acima do necessário (não só N+1) de propósito — autocorrige o histórico caso uma
    // rotação anterior tenha falhado no meio (crash, sem rede), em vez de deixar sobras
    // acumularem pra sempre sem nenhum cliente futuro conseguir enxergar/limpar o excedente.
    private const int TrimQueryLimit = 50;

    private static async Task TrimOldReplaysAsync(CollectionReference collection)
    {
        QuerySnapshot snap = await collection.OrderByDescending("createdAtTicks").Limit(TrimQueryLimit).GetSnapshotAsync();
        if (snap.Count <= MaxReplaysPerCharacter) return;

        var docs = new List<DocumentSnapshot>(snap.Documents);
        for (int i = MaxReplaysPerCharacter; i < docs.Count; i++)
            await docs[i].Reference.DeleteAsync();
    }
}
