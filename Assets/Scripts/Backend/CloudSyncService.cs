using System.Threading.Tasks;

// Sincronização "último gravado ganha" entre o save local (LocalSaveService) e o save na nuvem
// (FirestoreService) pra UM PlayerProfile — extraído de LoginController.SyncCharacterRoutine
// (2026-07-15, Fatia 4) pra ser reaproveitado em qualquer ponto que troca o personagem ativo, não
// só no login. Compara updatedAtTicks (relógio do cliente, simplificação já assumida no plano;
// ver comentário em CharacterDTOMap.ToMap) e aplica o mais recente dos dois.
//
// Fatia 4 (2026-07-15) é escopo mínimo por decisão do usuário: só estende a sincronização pra
// qualquer personagem trocado via grid (CharacterSelectController)/setas rápidas
// (MainMenuCharacterPreview) já existentes — NÃO inclui criar personagem novo do zero
// (characterTemplateId, CharacterCreation.GenerateLevel1Stats), isso fica pra decisão futura.
public static class CloudSyncService
{
    // Sem sessão ativa ou sem profile, não faz nada (comportamento local de sempre,
    // preservado). Idempotente e seguro de chamar toda vez que o personagem ativo muda.
    public static async Task SyncCharacterAsync(PlayerProfile profile)
    {
        if (profile == null || !AuthService.IsSignedIn) return;

        // Snapshot de fábrica do profile (2026-07-15, correção de isolamento entre contas) — tem
        // que ser a PRIMEIRA coisa depois do guard acima, antes de qualquer ApplyDTO nesta função
        // poder sobrescrever o profile com dado de conta nenhuma. Ver comentário completo em
        // PlayerProfileConverter.CapturePristineIfNeeded.
        PlayerProfileConverter.CapturePristineIfNeeded(profile);

        string uid = AuthService.CurrentUser.UserId;
        string characterId = profile.OpponentId();

        var cloudDto = await FirestoreService.LoadCharacterAsync(uid, characterId);
        long localTicks = LocalSaveService.GetCachedUpdatedAtTicks(characterId);

        if (cloudDto != null && cloudDto.updatedAtTicks >= localTicks)
        {
            // Nuvem é igual ou mais nova — aplica por cima do profile local.
            PlayerProfileConverter.ApplyDTO(profile, cloudDto);
            PlayerProfileConverter.MarkOwnerScope(profile, uid);
        }

        // BUG REAL CORRIGIDO (2026-07-15, 2ª rodada de isolamento entre contas): este Save() era
        // incondicional aqui - rodava mesmo quando não havia NENHUMA evidência de que o estado em
        // memória do profile pertencia a esta conta (sem doc na nuvem, sem cache local desta
        // conta). Bastava esta função rodar uma vez (todo login chama isto) com QUALQUER resíduo
        // em memória (de outra conta, de uma sessão anterior, etc.) pra esse resíduo ser gravado
        // como se fosse progresso real da conta atual — criando um personagem "fantasma" pra
        // contas novas que nunca escolheram/jogaram aquele personagem (reportado pelo usuário:
        // conta nova recebeu um personagem em level intermediário, nem o de fábrica nem o
        // realmente jogado). Só grava agora se pudermos AFIRMAR que este estado pertence à conta
        // atual - já veio da nuvem dela (linha acima), já era dela desde antes nesta sessão
        // (Save/ApplyIfSaved já marcaram, ver PlayerProfileConverter._ownerScope), está
        // genuinamente intocado (null - nunca reivindicado, ok estabelecer esta conta como
        // primeira dona), ou é progresso feito offline antes de qualquer login (OfflineScope -
        // reivindicável pela 1ª conta real que sincronizar, comportamento de single-device já
        // aceito no plano de contas).
        string ownerScope = PlayerProfileConverter.GetOwnerScope(profile);
        bool safeToSave = ownerScope == null || ownerScope == uid || ownerScope == LocalSaveService.OfflineScope;
        if (safeToSave)
        {
            PlayerProfileConverter.MarkOwnerScope(profile, uid);
            LocalSaveService.Save(profile);
        }
    }
}
