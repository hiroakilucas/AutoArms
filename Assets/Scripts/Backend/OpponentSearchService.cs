using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Busca de adversários reais na nuvem (Fatia 6, 2026-07-15) — lê opponents_index (Fatia 5),
// priorizando level próximo (levelBucket, ver FetchOpponentsAsync) e reconstrói cada resultado
// como um PlayerProfile runtime via PlayerProfileConverter.FromOpponentIndexMap. Chamador
// (SelectOpponentController) decide o que fazer se a lista vier incompleta — completa com bots
// (CharacterDatabase.botTemplates/BotProfileGenerator) antes de cair pro pool local antigo
// (CharacterDatabase.opponentCharacters), sem mudança nesse último caminho.
public static class OpponentSearchService
{
    // Firestore não tem "N aleatórios" nativo — cada documento em opponents_index carrega um
    // randomSeed (float 0..1, gravado em FirestoreService.SaveOpponentIndexAsync) sorteado a
    // cada save. Sorteando um limiar r e pegando metade dos resultados >= r e metade < r (cada
    // lado ordenado a partir de r, não do começo/fim da coleção) dá uma amostra que varia a cada
    // busca sem precisar buscar a coleção inteira.
    public static async Task<List<PlayerProfile>> FetchOpponentsAsync(CharacterDatabase templateCatalog, int myLevel, int count = 6)
    {
        var results = new List<PlayerProfile>();
        if (!AuthService.IsSignedIn || templateCatalog == null) return results;

        try
        {
            // Usa a instância do Firestore direto (sem passar por FirestoreService) — chama o
            // mesmo setup de persistência que FirestoreService.SaveXAsync chamaria, na ordem
            // certa (antes de qualquer Collection/Query abaixo). Sem isso, quando esta busca roda
            // antes de qualquer save da sessão (ex: 1ª luta depois de pular o login, ou Play Mode
            // iniciado direto numa cena que não passa por 00_Login), o SDK já considerava a
            // instância "em uso" e a 1ª tentativa de habilitar PersistenceEnabled (dentro de
            // FirestoreService.SaveCharacterAsync/etc, no fim do combate) lançava exceção e
            // derrubava TODA gravação da sessão — ver comentário completo em
            // FirestoreService.EnsurePersistence.
            FirestoreService.TryEnsurePersistence();

            string myUid = AuthService.CurrentUser.UserId;
            var col = FirebaseFirestore.DefaultInstance.Collection("opponents_index");
            float r = UnityEngine.Random.value;

            // Prioriza level próximo (2026-07-15) — levelBucket = (level/5)*5, já gravado por
            // FirestoreService.SaveOpponentIndexAsync mas nunca lido até agora. "Próximo o
            // suficiente" = mesmo bucket de 5 níveis ou um adjacente (~9 níveis de distância no
            // pior caso) — WhereIn cobre os 3 de uma vez (até 10 valores suportados pelo
            // Firestore), sem precisar de 3 buscas separadas. Requer índice composto
            // (levelBucket + randomSeed) no Firestore — a 1ª execução loga um erro com link direto
            // pra criar o índice, se ainda não existir.
            int myBucket = Mathf.Max(0, (myLevel / 5) * 5);
            var buckets = new List<object>();
            foreach (int b in new[] { myBucket, myBucket - 5, myBucket + 5 })
                if (b >= 0 && !buckets.Contains(b)) buckets.Add(b);

            // Busca mais documentos crus do que `count` de propósito (2026-07-15, correção de bug
            // real — antes buscava exatamente `count`, e SÓ DEPOIS excluía o próprio jogador/
            // resultados sem molde resolvido, então a lista final podia vir menor que `count`
            // mesmo havendo mais oponentes válidos no Firestore, nunca chegados a buscar; passava
            // despercebido só porque o pool de contas de teste ainda é pequeno). `FetchLimit` dá
            // folga suficiente pra sobreviver a 1 auto-exclusão + alguns moldes não resolvidos sem
            // precisar de paginação de verdade — reavaliar se o pool de jogadores crescer muito.
            int fetchLimit = count + 5;
            var maps = new List<Dictionary<string, object>>();

            var upperSnap = await col
                .WhereIn("levelBucket", buckets)
                .WhereGreaterThanOrEqualTo("randomSeed", r)
                .OrderBy("randomSeed")
                .Limit(fetchLimit)
                .GetSnapshotAsync();
            foreach (var doc in upperSnap.Documents) maps.Add(doc.ToDictionary());

            if (maps.Count < fetchLimit)
            {
                var lowerSnap = await col
                    .WhereIn("levelBucket", buckets)
                    .WhereLessThan("randomSeed", r)
                    .OrderByDescending("randomSeed")
                    .Limit(fetchLimit - maps.Count)
                    .GetSnapshotAsync();
                foreach (var doc in lowerSnap.Documents) maps.Add(doc.ToDictionary());
            }

            foreach (var map in maps)
            {
                // Nunca listar o próprio jogador como adversário de si mesmo.
                if (map.TryGetValue("ownerUid", out var ownerObj) && ownerObj as string == myUid) continue;

                var profile = PlayerProfileConverter.FromOpponentIndexMap(map, templateCatalog);
                if (profile != null) results.Add(profile);
                if (results.Count >= count) break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[OpponentSearchService] Falha ao buscar adversários: {e.Message}");
        }

        return results;
    }
}
