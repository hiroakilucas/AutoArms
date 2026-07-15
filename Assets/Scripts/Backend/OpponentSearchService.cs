using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Busca de adversários reais na nuvem (Fatia 6, 2026-07-15) — lê opponents_index (Fatia 5) e
// reconstrói cada resultado como um PlayerProfile runtime via
// PlayerProfileConverter.FromOpponentIndexMap. Chamador (SelectOpponentController) decide o que
// fazer se a lista vier vazia (sem sessão, sem rede, ou nenhum documento ainda) — cai pro pool
// local de sempre (CharacterDatabase.opponentCharacters), sem mudança nesse caminho.
public static class OpponentSearchService
{
    // Firestore não tem "N aleatórios" nativo — cada documento em opponents_index carrega um
    // randomSeed (float 0..1, gravado em FirestoreService.SaveOpponentIndexAsync) sorteado a
    // cada save. Sorteando um limiar r e pegando metade dos resultados >= r e metade < r (cada
    // lado ordenado a partir de r, não do começo/fim da coleção) dá uma amostra que varia a cada
    // busca sem precisar buscar a coleção inteira.
    public static async Task<List<PlayerProfile>> FetchOpponentsAsync(CharacterDatabase templateCatalog, int count = 6)
    {
        var results = new List<PlayerProfile>();
        if (!AuthService.IsSignedIn || templateCatalog == null) return results;

        try
        {
            string myUid = AuthService.CurrentUser.UserId;
            var col = FirebaseFirestore.DefaultInstance.Collection("opponents_index");
            float r = UnityEngine.Random.value;

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
                .WhereGreaterThanOrEqualTo("randomSeed", r)
                .OrderBy("randomSeed")
                .Limit(fetchLimit)
                .GetSnapshotAsync();
            foreach (var doc in upperSnap.Documents) maps.Add(doc.ToDictionary());

            if (maps.Count < fetchLimit)
            {
                var lowerSnap = await col
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
