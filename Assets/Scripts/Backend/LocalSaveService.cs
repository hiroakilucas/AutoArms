using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Corrige o bug real de save quebrado em build (2026-07-14): a progressao do jogador (level, XP,
// skills/armas ganhas, favoritos, batalhas restantes) so era gravada via
// UnityEditor.EditorUtility.SetDirty(profile), que e editor-only (#if UNITY_EDITOR) e nao faz
// nada num build de verdade - ou seja, num build real, NENHUM progresso persistia entre sessoes.
// Este servico grava/le um snapshot em JSON puro em Application.persistentDataPath, que funciona
// em qualquer plataforma (Editor, Windows, Android, iOS). Conversao PlayerProfile<->CharacterDTO
// mora em PlayerProfileConverter.cs (2026-07-15, Fatia 3 - extraida daqui pra ser compartilhada
// com o save na nuvem no Firestore).
//
// DIVIDA TECNICA CONHECIDA, registrada por pedido explicito do usuario (2026-07-14): o arquivo
// gravado aqui (save.json) e TEXTO PLANO, editavel por qualquer editor de texto (Windows/Editor)
// ou save editor generico (Android). NAO E SEGURO CONTRA EDICAO LOCAL. Decisao consciente por
// agora: NAO ofuscar/criptografar - nao compensa o esforco nesta fase e nao impede alguem
// determinado de qualquer jeito. Revisitar quando houver ranking competitivo de verdade que
// valha a pena proteger (ver ARQUITETURA.md). Nao tratar isso como "ja resolvido".
//
// Acoplamento com Firebase (2026-07-15, Fatia 3): Save() agora tambem empurra pro Firestore em
// segundo plano (fire-and-forget, sem bloquear os 5 call sites sincronos ja existentes -
// XpSystem/AttackSequencer/CharacterCardUI/CombatResultPanel) quando ha um usuario logado. Isso
// acopla este arquivo a AuthService/FirestoreService - decisao consciente: o save local deixou
// de precisar ser 100% independente do Firebase a partir desta fatia (o Firebase ja e parte
// permanente do projeto), e o acoplamento e so nesta direcao (LocalSaveService sabe do
// Firestore; Firestore/Auth nao sabem de LocalSaveService) - o save local continua funcionando
// sozinho, sem rede/sem login, exatamente como antes.
//
// BUG REAL CORRIGIDO (2026-07-15, isolamento entre contas): a chave do cache/save.json era só
// characterId, SEM vínculo de conta - qualquer conta que logasse no mesmo device/executável
// herdava o save.json deixado por outra conta que tivesse jogado o mesmo personagem antes
// (reportado pelo usuário: conta nova veio com os personagens/progresso de uma conta antiga já
// testada no mesmo build). Pior ainda: como CloudSyncService trata "sem save local, sem dado na
// nuvem" como "empurra o que tiver pra nuvem", isso podia GRAVAR o progresso da conta errada no
// Firestore da conta nova. Corrigido: a chave agora é "{accountScope}:{characterId}", onde
// accountScope = uid do Firebase Auth de quem salvou (ou "offline" sem sessão). Entradas do
// save.json gravadas ANTES desta correção (accountScope vazio) são tratadas como órfãs/não
// confiáveis e NUNCA aplicadas a nenhuma conta - ver EnsureLoaded.
public static class LocalSaveService
{
    private const string FileName = "save.json";

    // Público (2026-07-15, 2ª rodada da correção de isolamento) - CloudSyncService precisa
    // comparar contra este valor pra tratar progresso feito offline como "reivindicável" pela
    // 1ª conta real que sincronizar aquele profile (ver comentário em
    // PlayerProfileConverter._ownerScope).
    public const string OfflineScope = "offline";

    private static Dictionary<string, CharacterDTO> _cache;

    // JsonUtility nao serializa Dictionary/List no nivel raiz - precisa de uma classe wrapper.
    [Serializable]
    private class SaveFile
    {
        public List<CharacterDTO> characters = new List<CharacterDTO>();
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // uid da conta logada, ou um valor fixo pra "ninguém logado" — nunca vazio/nulo, pra sempre
    // dar uma chave de cache válida e nunca colidir acidentalmente entre "offline" e um uid real.
    private static string CurrentScope() =>
        AuthService.IsSignedIn ? AuthService.CurrentUser.UserId : OfflineScope;

    private static string CacheKey(string accountScope, string characterId) =>
        $"{accountScope}:{characterId}";

    private static void EnsureLoaded()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, CharacterDTO>();
        if (!File.Exists(FilePath)) return;

        try
        {
            string json = File.ReadAllText(FilePath);
            var save = JsonUtility.FromJson<SaveFile>(json);
            if (save?.characters == null) return;
            foreach (var dto in save.characters)
            {
                if (dto == null || string.IsNullOrEmpty(dto.characterId)) continue;
                // Entrada órfã (gravada antes da correção de isolamento por conta) - descartada
                // de propósito, nunca aplicada a nenhuma conta. Some do arquivo sozinha na
                // próxima vez que WriteToDisk() rodar, já que nunca entra em _cache.
                if (string.IsNullOrEmpty(dto.accountScope)) continue;
                _cache[CacheKey(dto.accountScope, dto.characterId)] = dto;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveService] Falha ao ler {FilePath}: {e.Message}");
        }
    }

    private static void WriteToDisk()
    {
        var save = new SaveFile { characters = new List<CharacterDTO>(_cache.Values) };
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(save, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveService] Falha ao gravar {FilePath}: {e.Message}");
        }
    }

    // Grava um snapshot COMPLETO (nao um diff) da progressao atual do profile - chamado pelos
    // mesmos pontos que antes so chamavam EditorUtility.SetDirty. Tambem empurra pro Firestore em
    // segundo plano se houver sessao ativa (ver comentario de acoplamento no topo do arquivo).
    public static void Save(PlayerProfile profile)
    {
        if (profile == null) return;
        EnsureLoaded();

        var dto = PlayerProfileConverter.ToDTO(profile);
        dto.accountScope = CurrentScope();
        _cache[CacheKey(dto.accountScope, dto.characterId)] = dto;
        WriteToDisk();
        // Estabelece/reafirma quem é "dono" deste estado em memória agora (2ª rodada da correção
        // de isolamento) - ver PlayerProfileConverter._ownerScope.
        PlayerProfileConverter.MarkOwnerScope(profile, dto.accountScope);

        if (AuthService.IsSignedIn)
        {
            // Fire-and-forget de propósito - Save() é chamado de código síncrono (handlers de UI,
            // fim de combate) que não pode virar async sem mudar as 5 call sites existentes. Erros
            // já são logados dentro de FirestoreService.SaveCharacterAsync/SaveOpponentIndexAsync.
            string uid = AuthService.CurrentUser.UserId;
            _ = FirestoreService.SaveCharacterAsync(uid, dto);
            // opponents_index (Fatia 5, 2026-07-15) — escrito no MESMO momento que o save base
            // acima, com o mesmo `profile`/`dto` (ver ARQUITETURA.md, "Stat base vs. stat
            // efetivo") — evita os dois documentos ficarem dessincronizados entre si.
            _ = FirestoreService.SaveOpponentIndexAsync(uid, profile, dto);
        }
    }

    // Aplica o save salvo (se existir) por cima do profile - idempotente e seguro de chamar
    // repetidamente (sempre reflete o ultimo estado conhecido em cache/disco). Chamar sempre que
    // um PlayerProfile for exibido/usado de verdade pela primeira vez na sessao (grid de
    // personagens, preview do menu, inicio de combate) - ver call sites em
    // CharacterCardUI/MainMenuCharacterPreview/CombatSceneLoader. Escopado pela conta ATUAL
    // (CurrentScope()) - o save de outra conta pro mesmo personagem simplesmente não é
    // encontrado, nunca aplicado por engano.
    public static void ApplyIfSaved(PlayerProfile profile)
    {
        if (profile == null) return;

        // Snapshot de fábrica (2026-07-15, correção de isolamento entre contas) - captura ANTES
        // de aplicar qualquer dado escopado por conta. Este é, na prática, o ponto de entrada mais
        // frequente onde um PlayerProfile é tocado pela primeira vez numa sessão do processo
        // (CombatSceneLoader/CharacterCardUI/MainMenuController chamam isto bem mais vezes do que
        // CloudSyncService.SyncCharacterAsync chama ApplyDTO direto) - sem capturar aqui também, a
        // maioria dos profiles nunca teria um snapshot pristine registrado antes de já terem
        // recebido dado de conta, e RestoreAllPristine() não teria o que restaurar pra eles no
        // logout. Ver PlayerProfileConverter.CapturePristineIfNeeded.
        PlayerProfileConverter.CapturePristineIfNeeded(profile);
        EnsureLoaded();

        string scope = CurrentScope();
        string key = CacheKey(scope, profile.OpponentId());
        if (!_cache.TryGetValue(key, out var dto)) return;
        PlayerProfileConverter.ApplyDTO(profile, dto);
        // Achou save local desta conta pra este personagem = dado legítimo dela - marca posse
        // (2ª rodada da correção de isolamento, ver PlayerProfileConverter._ownerScope).
        PlayerProfileConverter.MarkOwnerScope(profile, scope);
    }

    // Timestamp (DateTime.UtcNow.Ticks) do save local em cache pra esta chave NA CONTA ATUAL, ou
    // 0 se não houver nenhum - usado por CloudSyncService pra decidir se a versão da nuvem é mais
    // nova que a local (reconciliação "último gravado ganha", ver ARQUITETURA.md/plano de
    // contas). Escopado por conta pelo mesmo motivo de ApplyIfSaved acima.
    public static long GetCachedUpdatedAtTicks(string characterId)
    {
        EnsureLoaded();
        string key = CacheKey(CurrentScope(), characterId);
        return _cache.TryGetValue(key, out var dto) ? dto.updatedAtTicks : 0L;
    }
}
