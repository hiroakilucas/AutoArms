using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Carteira de moeda geral (coins) e diamante (moeda premium) — POR CONTA, compartilhada entre
// todos os personagens do jogador (users/{uid}, não dentro de users/{uid}/characters/{characterId}
// como a energia — ver EnergyService.cs).
//
// SEGURANÇA — DIAMANTE (placeholder, 2026-07-19): a regra permanente do projeto (ver
// ARQUITETURA.md "Moeda premium") é diamante NUNCA client-writable, sempre via Cloud Function
// validando a intenção antes de gravar. Este projeto ainda não tem nenhuma Cloud Function
// implantada — decisão explícita do usuário ao encomendar o sistema de energia: entregar o
// HUD/gate agora com este placeholder client-writable (mesmo modelo "cliente confiável" que
// level/str/etc já usam hoje), e endurecer quando a Fase 4/Monetização criar o projeto de
// Functions de verdade. SpendDiamondsAsync grava direto do cliente — TODO de segurança: trocar
// por uma Cloud Function callable assim que ela existir, sem precisar mudar a assinatura pro
// chamador (MainMenuController já trata isto como "intenção de gastar", não como um cálculo de
// saldo que o cliente possa forjar livremente pra outros fins).
public static class WalletService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static DocumentReference WalletDoc(string uid) =>
        Db.Collection("users").Document(uid);

    public static async Task<(int coins, int diamonds)> LoadAsync(string uid)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await WalletDoc(uid).GetSnapshotAsync();
            int coins = 0, diamonds = 0;
            if (snap.Exists)
            {
                if (snap.TryGetValue<long>("coins", out var c)) coins = (int)c;
                if (snap.TryGetValue<long>("diamonds", out var d)) diamonds = (int)d;
            }
            return (coins, diamonds);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WalletService] Falha ao carregar carteira de '{uid}': {e.Message}");
            return (0, 0);
        }
    }

    // "Próximo Personagem" (2026-07-26) — só LEITURA do contador persistido pela Cloud Function
    // purchaseNextCharacter (users/{uid}.nextCharacterPurchaseCount); o cliente nunca escreve
    // este campo (mesma regra de "moeda premium", aplicada aqui porque o contador decide preço,
    // não só exibição). Usado por ShopController pra mostrar o preço real da próxima compra antes
    // do 1º clique da sessão.
    public static async Task<int> LoadNextCharacterPurchaseCountAsync(string uid)
    {
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await WalletDoc(uid).GetSnapshotAsync();
            if (snap.Exists && snap.TryGetValue<long>("nextCharacterPurchaseCount", out var count)) return (int)count;
            return 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WalletService] Falha ao carregar nextCharacterPurchaseCount de '{uid}': {e.Message}");
            return 0;
        }
    }

    public static async Task<bool> AddCoinsAsync(string uid, int amount)
    {
        if (amount == 0) return true;
        try
        {
            FirestoreService.TryEnsurePersistence();
            await WalletDoc(uid).SetAsync(
                new Dictionary<string, object> { { "coins", FieldValue.Increment((long)amount) } },
                SetOptions.MergeAll);
            PlayerEconomyState.Coins += amount;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WalletService] Falha ao creditar {amount} moeda(s) pra '{uid}': {e.Message}");
            return false;
        }
    }

    // Crédito de diamante (2026-07-21, Loja — compra de pacote) — mesmo padrão de AddCoinsAsync
    // acima. Ver aviso de segurança no topo do arquivo: placeholder client-writable, sem Cloud
    // Function ainda (ARQUITETURA.md "Moeda premium" — toda gravação de diamante deveria passar
    // por uma function validando a intenção de compra/recibo antes de creditar; aqui o cliente
    // credita direto, mesmo modelo "cliente confiável" do resto do projeto nesta fase). Migrar
    // junto de SpendDiamondsAsync quando a Fase 8/gateway de pagamento real chegar.
    public static async Task<bool> AddDiamondsAsync(string uid, int amount)
    {
        if (amount == 0) return true;
        try
        {
            FirestoreService.TryEnsurePersistence();
            await WalletDoc(uid).SetAsync(
                new Dictionary<string, object> { { "diamonds", FieldValue.Increment((long)amount) } },
                SetOptions.MergeAll);
            PlayerEconomyState.Diamonds += amount;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WalletService] Falha ao creditar {amount} diamante(s) pra '{uid}': {e.Message}");
            return false;
        }
    }

    // Ver aviso de segurança no topo do arquivo — placeholder client-writable, não é a versão
    // final. O chamador (MainMenuController) já garante amount <= PlayerEconomyState.Diamonds
    // antes de chamar isto; mesmo assim nunca deixa o saldo LOCAL cacheado ir negativo aqui.
    public static async Task<bool> SpendDiamondsAsync(string uid, int amount)
    {
        if (amount <= 0) return true;
        try
        {
            FirestoreService.TryEnsurePersistence();
            await WalletDoc(uid).SetAsync(
                new Dictionary<string, object> { { "diamonds", FieldValue.Increment((long)-amount) } },
                SetOptions.MergeAll);
            PlayerEconomyState.Diamonds = Mathf.Max(0, PlayerEconomyState.Diamonds - amount);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WalletService] Falha ao gastar {amount} diamante(s) de '{uid}': {e.Message}");
            return false;
        }
    }
}
