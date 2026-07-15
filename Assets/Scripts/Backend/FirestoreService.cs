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
    private static void EnsurePersistence()
    {
        if (_persistenceConfigured) return;
        Db.Settings.PersistenceEnabled = true;
        _persistenceConfigured = true;
    }

    private static DocumentReference CharacterDoc(string uid, string characterId) =>
        Db.Collection("users").Document(uid).Collection("characters").Document(characterId);

    public static async Task<(bool success, string error)> SaveCharacterAsync(string uid, CharacterDTO dto)
    {
        try
        {
            EnsurePersistence();
            await CharacterDoc(uid, dto.characterId).SetAsync(CharacterDTOMap.ToMap(dto));
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
}
