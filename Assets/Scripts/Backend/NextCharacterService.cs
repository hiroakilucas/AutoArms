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
