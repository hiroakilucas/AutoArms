using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Persistência real das compras da Loja (2026-07-21, substitui o estado fake local que resetava
// a cada sessão — ver PlayerUnlocksState/PlayerPassState/PlayerProgressionState, cujos
// comentários de "TODO SEGURANÇA... sem persistência real" ficam desatualizados a partir daqui).
// Mesmo documento users/{uid} já usado por WalletService (coins/diamonds) — campos FLAT no
// mesmo nível (não aninhados em unlocks/passSubscription/progressionSlots como um desenho
// hipotético poderia sugerir), pra bater com o estilo já estabelecido nesse documento e evitar a
// ambiguidade de merge de mapas aninhados do Firestore (ver MarkPackageFirstPurchaseUsedAsync
// pra explicação de por que purchasedPackages, que É um mapa, precisa de SetOptions.MergeFields
// em vez de MergeAll). "passSubscription.tier" (none/basico/pro) do desenho original NÃO foi
// usado de propósito — o modelo de Passe já foi redesenhado (2026-07-21) pra Básico/Pro
// independentes com contador de dias próprio cada; persistir um "tier único" reintroduziria o
// modelo antigo que o próprio usuário pediu pra abandonar.
//
// TODO SEGURANÇA (mesmo padrão de WalletService.SpendDiamondsAsync/AddDiamondsAsync — ver
// ARQUITETURA.md "Moeda premium"): toda escrita aqui é client-writable, sem Cloud Function ainda.
// Os campos deste arquivo (desbloqueios/passe/progressão/pacotes-já-usados) não movem diamante
// diretamente, mas a "intenção de compra" que os aciona (ShopController.OnBuyClicked) confia
// inteiramente no cliente — migrar pra Cloud Function junto do resto da Loja quando a Fase 8/
// gateway de pagamento real chegar.
public static class ShopStateService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static DocumentReference UserDoc(string uid) => Db.Collection("users").Document(uid);

    // Carrega tudo de uma vez (chamado ao abrir a Loja) e espelha nos canais estáticos de sempre
    // — ShopController só precisa reconstruir a UI a partir de PlayerUnlocksState/PlayerPassState/
    // PlayerProgressionState/PlayerEconomyState.UsedFirstPurchaseBonusAmounts depois, exatamente
    // como já fazia com o estado fake (nenhuma lógica de exibição precisou mudar).
    public static async Task LoadAsync(string uid)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await UserDoc(uid).GetSnapshotAsync();
            if (!snap.Exists) return;

            var data = snap.ToDictionary();

            if (data.TryGetValue("skipUnlocked", out var skipObj) && skipObj is bool skip)
                PlayerUnlocksState.SkipUnlocked = skip;
            if (data.TryGetValue("speed15xUnlocked", out var speedObj) && speedObj is bool speed)
                PlayerUnlocksState.Speed15xUnlocked = speed;

            if (data.TryGetValue("passBasicoDaysRemaining", out var basicoObj) && basicoObj is long basico)
                PlayerPassState.BasicoDaysRemaining = (int)basico;
            if (data.TryGetValue("passProDaysRemaining", out var proObj) && proObj is long pro)
                PlayerPassState.ProDaysRemaining = (int)pro;

            if (data.TryGetValue("progressionSlot1Unlocked", out var s1Obj) && s1Obj is bool s1)
                PlayerProgressionState.Slot1Unlocked = s1;
            if (data.TryGetValue("progressionSlot2Unlocked", out var s2Obj) && s2Obj is bool s2)
                PlayerProgressionState.Slot2Unlocked = s2;
            if (data.TryGetValue("progressionSlot3Unlocked", out var s3Obj) && s3Obj is bool s3)
                PlayerProgressionState.Slot3Unlocked = s3;

            PlayerEconomyState.UsedFirstPurchaseBonusAmounts.Clear();
            if (data.TryGetValue("purchasedPackages", out var pkgObj) && pkgObj is Dictionary<string, object> packages)
            {
                foreach (var kv in packages)
                    if (kv.Value is bool used && used && int.TryParse(kv.Key, out int amount))
                        PlayerEconomyState.UsedFirstPurchaseBonusAmounts.Add(amount);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShopStateService] Falha ao carregar estado da Loja de '{uid}': {e.Message}");
        }
    }

    public static async Task<bool> SetSkipUnlockedAsync(string uid)
    {
        PlayerUnlocksState.SkipUnlocked = true;
        return await WriteFlagAsync(uid, "skipUnlocked");
    }

    public static async Task<bool> SetSpeed15xUnlockedAsync(string uid)
    {
        PlayerUnlocksState.Speed15xUnlocked = true;
        return await WriteFlagAsync(uid, "speed15xUnlocked");
    }

    // Bundle liga os dois flags numa escrita só.
    public static async Task<bool> SetBundleUnlockedAsync(string uid)
    {
        PlayerUnlocksState.SkipUnlocked = true;
        PlayerUnlocksState.Speed15xUnlocked = true;
        try
        {
            FirestoreService.TryEnsurePersistence();
            await UserDoc(uid).SetAsync(
                new Dictionary<string, object> { { "skipUnlocked", true }, { "speed15xUnlocked", true } },
                SetOptions.MergeAll);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShopStateService] Falha ao gravar Bundle pra '{uid}': {e.Message}");
            return false;
        }
    }

    private static async Task<bool> WriteFlagAsync(string uid, string field)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            await UserDoc(uid).SetAsync(new Dictionary<string, object> { { field, true } }, SetOptions.MergeAll);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShopStateService] Falha ao gravar '{field}' pra '{uid}': {e.Message}");
            return false;
        }
    }

    // Básico e Pro são independentes (ver PlayerPassState) — cada compra soma +30 dias no
    // contador PRÓPRIO daquele passe. FieldValue.Increment é seguro pra campo escalar top-level
    // via SetOptions.MergeAll (diferente do mapa purchasedPackages abaixo).
    public static async Task<bool> AddPassDaysAsync(string uid, bool isPro, int days)
    {
        if (isPro) PlayerPassState.ProDaysRemaining += days;
        else PlayerPassState.BasicoDaysRemaining += days;

        string field = isPro ? "passProDaysRemaining" : "passBasicoDaysRemaining";
        try
        {
            FirestoreService.TryEnsurePersistence();
            await UserDoc(uid).SetAsync(
                new Dictionary<string, object> { { field, FieldValue.Increment((long)days) } },
                SetOptions.MergeAll);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShopStateService] Falha ao somar {days} dia(s) de passe ({field}) pra '{uid}': {e.Message}");
            return false;
        }
    }

    public static async Task<bool> UnlockProgressionSlotAsync(string uid, int slotIndex)
    {
        string field;
        switch (slotIndex)
        {
            case 1: PlayerProgressionState.Slot1Unlocked = true; field = "progressionSlot1Unlocked"; break;
            case 2: PlayerProgressionState.Slot2Unlocked = true; field = "progressionSlot2Unlocked"; break;
            case 3: PlayerProgressionState.Slot3Unlocked = true; field = "progressionSlot3Unlocked"; break;
            default: return false;
        }
        return await WriteFlagAsync(uid, field);
    }

    // Mapa pacoteId(string) -> bool ("bônus de 1ª compra já usado"). SetOptions.MergeFields (não
    // MergeAll) com o caminho pontilhado exato "purchasedPackages.{amount}" — MergeAll sozinho,
    // pra um CAMPO cujo valor é um mapa, substitui o mapa inteiro pelo objeto dado (perderia
    // qualquer pacote já marcado antes que não estivesse neste payload); o caminho pontilhado
    // garante que só essa UMA chave do mapa seja escrita/mesclada, preservando as demais.
    public static async Task<bool> MarkPackageFirstPurchaseUsedAsync(string uid, int packageAmount)
    {
        PlayerEconomyState.UsedFirstPurchaseBonusAmounts.Add(packageAmount);
        string fieldPath = $"purchasedPackages.{packageAmount}";
        try
        {
            FirestoreService.TryEnsurePersistence();
            var data = new Dictionary<string, object>
            {
                { "purchasedPackages", new Dictionary<string, object> { { packageAmount.ToString(), true } } },
            };
            await UserDoc(uid).SetAsync(data, SetOptions.MergeFields(new[] { fieldPath }));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShopStateService] Falha ao marcar bônus de 1ª compra do pacote {packageAmount} pra '{uid}': {e.Message}");
            return false;
        }
    }
}
