using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `purchaseNextCharacter` (2026-07-26) — "Próximo Personagem" (Coins),
// substitui "Case Geral" (diamante) na aba Personagens da Loja. Mesmo princípio de
// CaseService/UnlockRerollService: nunca sorteia nada localmente nem decrementa moeda no
// cliente — só envia a INTENÇÃO de compra (sem payload, o preço é calculado 100% server-side a
// partir do contador persistido do jogador) e aplica o resultado que o servidor decidiu.
public static class NextCharacterService
{
    // Tabela de preço (Coins) da N-ésima compra de "Chibers Aleatório" (unlockNumber 1-based) —
    // extraída de ShopController (2026-07-27, era `CharacterSlotPriceTable`/`CharacterSlotCost`
    // privados de lá) pra ser reaproveitada também pelo indicador de "compra disponível" (bolinha
    // vermelha, ver CLAUDE.md) do botão Loja no Main Menu — sem essa extração, o Main Menu
    // precisaria duplicar a tabela pra saber o preço da próxima compra. Só pra EXIBIÇÃO — o preço
    // real/cobrança sempre é decidido em purchaseNextCharacter.ts (PRICE_TABLE lá), mantida em
    // sincronia manual com esta.
    public static readonly int[] PriceTable = { 25, 50, 100, 200, 400, 800, 1200, 1400, 1600, 1800, 2000, 2200 };
    private const int PriceStepAfterTable = 200;

    public static int NextPurchaseCost(int unlockNumber) => unlockNumber <= PriceTable.Length
        ? PriceTable[unlockNumber - 1]
        : PriceTable[PriceTable.Length - 1] + PriceStepAfterTable * (unlockNumber - PriceTable.Length);

    // Bolinha vermelha de "compra disponível" (2026-07-27, pedido do usuário) — verdadeiro quando
    // `PlayerEconomyState.Coins` (mesmo campo lido por qualquer outro HUD de moeda do jogo, nunca
    // recalculado de forma independente) já cobre o preço da PRÓXIMA compra. Lida diretamente do
    // canal estático em vez de receber parâmetro — mesmo padrão de leitura síncrona já usado por
    // CharacterPanel/MainMenuCharacterPreview pra desenhar HUD a partir de PlayerEconomyState.
    public static bool CanAffordNextPurchase() =>
        PlayerEconomyState.Coins >= NextPurchaseCost(PlayerEconomyState.NextCharacterPurchaseCount + 1);

    // Mesma região das outras Cloud Functions do projeto.
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public class NextCharacterPurchaseResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "failed-precondition" (saldo de moedas insuficiente, ou pool esgotada — ver
        // ErrorMessage pra distinguir), "internal".
        public string ErrorCode;
        public string ErrorMessage;

        public string WonCharacterTypeId;
        // ID do documento recém-criado em users/{uid}/characters — mesmo uso de
        // CasePurchaseResult.GrantedCharacterId (handoff CaseOpeningPopup → PendingCharacterSelection
        // → CharacterSelectController).
        public string GrantedCharacterId;
        public int CoinsSpent;
        public int NewCoinsBalance;
        public int NextPurchaseCost;
        public List<string> ReelPoolCharacterTypeIds = new List<string>();
    }

    public static async Task<NextCharacterPurchaseResult> PurchaseNextCharacterAsync()
    {
        try
        {
            var callable = Functions.GetHttpsCallable("purchaseNextCharacter");
            var response = await callable.CallAsync(new Dictionary<string, object>());

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[NextCharacterService] Resposta de purchaseNextCharacter em formato inesperado (não é um objeto).");
                return new NextCharacterPurchaseResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            var result = new NextCharacterPurchaseResult
            {
                Success = true,
                WonCharacterTypeId = dict["wonCharacterTypeId"] as string,
                GrantedCharacterId = dict["grantedCharacterId"] as string,
                CoinsSpent = Convert.ToInt32(dict["coinsSpent"]),
                NewCoinsBalance = Convert.ToInt32(dict["newCoinsBalance"]),
                NextPurchaseCost = Convert.ToInt32(dict["nextPurchaseCost"]),
            };

            if (dict["reelPoolCharacterTypeIds"] is IList poolList)
                foreach (var item in poolList)
                    if (item is string s) result.ReelPoolCharacterTypeIds.Add(s);

            return result;
        }
        catch (FunctionsException e)
        {
            string code = ToServerErrorCode(e.ErrorCode);
            Debug.LogError($"[NextCharacterService] purchaseNextCharacter falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");
            return new NextCharacterPurchaseResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[NextCharacterService] Falha ao comprar próximo personagem: {e.Message}");
            return new NextCharacterPurchaseResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de CaseService.ToServerErrorCode.
    private static string ToServerErrorCode(FunctionsErrorCode code)
    {
        switch (code)
        {
            case FunctionsErrorCode.Unauthenticated: return "unauthenticated";
            case FunctionsErrorCode.InvalidArgument: return "invalid-argument";
            case FunctionsErrorCode.NotFound: return "not-found";
            case FunctionsErrorCode.FailedPrecondition: return "failed-precondition";
            case FunctionsErrorCode.ResourceExhausted: return "resource-exhausted";
            case FunctionsErrorCode.PermissionDenied: return "permission-denied";
            case FunctionsErrorCode.Internal: return "internal";
            default: return code.ToString();
        }
    }
}
