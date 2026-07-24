using System;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Leitura do catálogo casePackages/ (2026-07-23, sistema de compra de personagens/case opening) -
// só leitura, nunca grava (allow write: if false no firestore.rules; população real é via
// functions/src/scripts/seedCasePackages.ts, Admin SDK). Usado pela Loja pra mostrar preço/limite
// de compra reais em vez de hardcoded - ver ARQUITETURA.md "Modelo de roster multi-personagem".
public static class CasePackageService
{
    public class CasePackageInfo
    {
        public string PackageId;
        public bool IsRarityLocked;
        public int RarityTier; // (int)CharacterRarity
        public double CashPrice;
        public int CurrencyCost;
        public int PurchaseLimitPerPlayer; // <=0 = sem limite
    }

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    public static async Task<CasePackageInfo> LoadPackageAsync(string packageId)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await Db.Collection("casePackages").Document(packageId).GetSnapshotAsync();
            if (!snap.Exists) return null;

            return new CasePackageInfo
            {
                PackageId = packageId,
                IsRarityLocked = snap.TryGetValue<bool>("isRarityLocked", out var locked) && locked,
                RarityTier = snap.TryGetValue<long>("rarityTier", out var rarity) ? (int)rarity : 0,
                // cashPrice é conceitualmente double, mas os preços de hoje são todos "inteiros"
                // (99.00/199.00/249.00) - o seed em Node (functions/src/scripts/seedCasePackages.ts)
                // pode gravar isso como INTEGER_VALUE no Firestore (99.0 === 99 em JS), então
                // TryGetValue<double> sozinho arriscaria falhar por mismatch de tipo. Lê via
                // System.Convert em cima do valor cru (object), que aceita qualquer tipo numérico
                // que o Firestore tenha usado - mesmo padrão defensivo de CharacterDTOMap.GetFloat.
                CashPrice = snap.TryGetValue<object>("cashPrice", out var cashRaw) && cashRaw != null ? Convert.ToDouble(cashRaw) : 0.0,
                CurrencyCost = snap.TryGetValue<long>("currencyCost", out var cost) ? (int)cost : 0,
                PurchaseLimitPerPlayer = snap.TryGetValue<long>("purchaseLimitPerPlayer", out var limit) ? (int)limit : 0,
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"[CasePackageService] Falha ao carregar pacote '{packageId}': {e.Message}");
            return null;
        }
    }

    // 0 se o jogador nunca comprou este pacote (documento não existe ainda) - mesma convenção de
    // "sem histórico ainda" usada no resto do projeto (ex: WalletService.LoadAsync).
    public static async Task<int> LoadPurchasedCountAsync(string uid, string packageId)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await Db.Collection("users").Document(uid)
                .Collection("casePurchases").Document(packageId).GetSnapshotAsync();
            if (!snap.Exists) return 0;
            return snap.TryGetValue<long>("purchasedCount", out var count) ? (int)count : 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CasePackageService] Falha ao carregar contador de compras '{packageId}' de '{uid}': {e.Message}");
            return 0;
        }
    }
}
