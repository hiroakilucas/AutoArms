using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Leitura do roster completo de personagens de uma conta (2026-07-23, sistema de compra de
// personagens/case opening - ver ARQUITETURA.md "Modelo de roster multi-personagem"). Só LEITURA
// - conceder um personagem novo acontece exclusivamente na Cloud Function purchaseCase (Admin
// SDK, nunca client-side); este serviço existe pra Loja conseguir mostrar "você já tem N
// personagens dessa raridade"/desabilitar compra sem precisar duplicar a query em cada tela.
// Checagem puramente cosmética - purchaseCase revalida server-side de qualquer forma (nunca
// confia no cliente pra decidir se a compra é permitida).
public static class RosterService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static CollectionReference CharactersCollection(string uid) =>
        Db.Collection("users").Document(uid).Collection("characters");

    // Cache de SESSÃO (2026-07-25, bug real corrigido — reportado pelo usuário: personagem
    // comprado aparecia desbloqueado logo após o case opening, mas voltava a aparecer bloqueado
    // ao reabrir `02_SelectCharacter` pelo menu principal, na MESMA sessão). `Source.Server`
    // sozinho (ver ListOwnedCharacterDocsAsync abaixo) não bastou — mesmo forçando o servidor,
    // não há garantia formal de que a QUERY DE LISTAGEM (agregada, paginação/índice internos do
    // SDK) sempre reflita um doc criado poucos segundos antes por outro processo (a Cloud
    // Function, via Admin SDK) tão rápido/consistentemente quanto um `get()` direto por ID.
    // Em vez de depender de qualquer garantia de consistência do SDK pra ISSO, este cache guarda
    // TUDO que este device já confirmou possuir nesta sessão (via listagem OU via get() direto) e
    // é sempre MESCLADO no resultado de `ListOwnedCharacterDocsAsync` — um personagem que a
    // listagem já trouxe uma vez nesta sessão nunca mais "desaparece" de uma leitura futura, não
    // importa o que uma query subsequente devolva. Atualizado a cada leitura bem-sucedida (a
    // versão mais recente sempre sobrescreve, então stats desatualizados de uma leitura antiga
    // não persistem além do necessário).
    //
    // Chave escopada por uid (2026-07-25, bug real corrigido — era só `characterId`, sem uid
    // nenhum) — "só reseta ao fechar o app" presumia que fechar o Play Mode já fechava o app de
    // fato (Domain Reload limpava todo estático de graça); depois de desligar o Domain Reload
    // (ver ProjectSettings/EditorSettings.asset) esse cache passou a sobreviver entre contas de
    // teste diferentes na MESMA sessão do Editor, inclusive quando a conta antiga é apagada direto
    // pelo Firebase Console (fluxo de LIMPEZA_BASE.md, que nunca passa por
    // MainMenuController.OnLogoutClicked/RestoreAllPristine) — um characterId cacheado da conta
    // antiga vazava pro roster mesclado da conta nova. Escopar por uid elimina o vazamento sem
    // depender de nenhum passo extra de limpeza.
    private static readonly Dictionary<(string uid, string characterId), CharacterDTO> SessionCache =
        new Dictionary<(string, string), CharacterDTO>();

    private static void CacheDto(string uid, CharacterDTO dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.characterId)) return;
        SessionCache[(uid, dto.characterId)] = dto;
    }

    // Lista todos os documentos de personagem da conta (o "original" pré-existente, sem
    // characterTypeId, e qualquer personagem concedido via case opening, com characterTypeId
    // preenchido) - já permitido pelas regras atuais do Firestore (`allow read` em
    // characters/{characterId} cobre tanto get quanto list, pra quem for o dono do uid).
    //
    // `Source.Server` explícito (2026-07-25) — o personagem é criado pela Cloud Function via
    // Admin SDK, que nunca passa pelos listeners/sync normais do SDK client-side; sem forçar o
    // servidor, esta query podia continuar devolvendo o cache local persistente
    // (`FirestoreService.PersistenceEnabled = true`) mesmo bem depois da compra. Complementado
    // pelo `SessionCache` acima (rede de segurança definitiva contra qualquer inconsistência
    // remanescente da própria query de listagem).
    public static async Task<List<CharacterDTO>> ListOwnedCharacterDocsAsync(string uid)
    {
        var result = new List<CharacterDTO>();
        var seenIds = new HashSet<string>();
        try
        {
            FirestoreService.TryEnsurePersistence();
            QuerySnapshot snap = await CharactersCollection(uid).GetSnapshotAsync(Source.Server);
            foreach (var doc in snap.Documents)
            {
                var dto = CharacterDTOMap.FromMap(doc.ToDictionary());
                CacheDto(uid, dto);
                if (!string.IsNullOrEmpty(dto.characterId)) seenIds.Add(dto.characterId);
                result.Add(dto);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RosterService] Falha ao listar roster de '{uid}': {e.Message}");
        }

        // Rede de segurança (ver comentário do SessionCache acima) — inclui qualquer personagem
        // já confirmado nesta sessão (desta MESMA conta) que a listagem acima não trouxe desta vez.
        foreach (var kv in SessionCache)
            if (kv.Key.uid == uid && !seenIds.Contains(kv.Key.characterId)) result.Add(kv.Value);

        return result;
    }

    // Busca UM documento específico por characterId (2026-07-25, ver CharacterSelectController.
    // ResolvePendingCharacterSelectionAsync) - fallback direto/pontual pro personagem recém-
    // concedido pelo case opening, decoupled da query de listagem inteira acima (que já deveria
    // incluir esse doc, mas isolar num get() direto elimina qualquer dependência de ordenação/
    // tamanho de página/timing da query de coleção pra este caso específico, onde só precisamos
    // de UM documento cujo ID já conhecemos de antemão). Retorna null se não existir/falhar.
    public static async Task<CharacterDTO> GetOwnedCharacterDocAsync(string uid, string characterId)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await CharactersCollection(uid).Document(characterId).GetSnapshotAsync(Source.Server);
            if (!snap.Exists) return null;
            var dto = CharacterDTOMap.FromMap(snap.ToDictionary());
            CacheDto(uid, dto); // ver SessionCache acima
            return dto;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RosterService] Falha ao buscar personagem '{characterId}' de '{uid}': {e.Message}");
            return null;
        }
    }

    // Conta quantos personagens já possuídos batem com uma raridade (int, (int)CharacterRarity) -
    // usado pra desabilitar/avisar num card de case de raridade travada antes mesmo de chamar a
    // Cloud Function (cosmético; a function é quem de fato decide "pool esgotada").
    public static int CountOwnedOfRarity(List<CharacterDTO> owned, int rarity)
    {
        int count = 0;
        foreach (var dto in owned)
            if (dto != null && dto.rarity == rarity) count++;
        return count;
    }
}
