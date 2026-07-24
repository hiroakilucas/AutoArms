using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `purchaseCase` (2026-07-23, sistema de compra de personagens/case
// opening - ver ARQUITETURA.md "Modelo de roster multi-personagem" e
// functions/src/purchaseCase.ts). Nunca sorteia nada localmente nem decrementa diamante/estoque
// no cliente - só envia a INTENÇÃO de compra e aplica o resultado que o servidor decidiu (mesmo
// princípio da regra "Moeda premium" em ARQUITETURA.md).
//
// PRÉ-REQUISITO: este arquivo referencia `Firebase.Functions`, pacote que ainda não está
// importado neste projeto (só Firebase.Firestore/Firebase.Auth/FirebaseApp existiam até
// 2026-07-23) - importar `FirebaseFunctions.unitypackage` da MESMA versão do Firebase Unity SDK
// já usada aqui antes de este arquivo compilar.
public static class CaseService
{
    // Precisa bater exatamente com a região onde purchaseCase está implantada
    // (functions/src/purchaseCase.ts, `onCall({ region: "southamerica-east1" }, ...)`).
    // FirebaseFunctions.DefaultInstance (sem região) aponta pra us-central1 por padrão — como a
    // function nunca foi implantada lá, a chamada falhava ANTES de chegar no servidor (nenhum
    // log aparecia no Cloud Logging de purchaseCase, só de deploy/inicialização do container;
    // bug real reportado pelo usuário, 2026-07-23: compra de "Personagem Legendary" sempre caindo
    // no popup genérico de erro). Se a region da function mudar no futuro, atualizar aqui também.
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public class CasePurchaseResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "invalid-argument", "not-found", "failed-precondition" (inclui "pool esgotada" e saldo
        // insuficiente - ver ErrorMessage pra distinguir), "resource-exhausted" (limite de
        // compras atingido), "internal" (falha inesperada).
        public string ErrorCode;
        public string ErrorMessage;

        public string WonCharacterTypeId;
        // ID do documento concedido em users/{uid}/characters (2026-07-24) — distinto de
        // WonCharacterTypeId (o molde); usado só pro handoff CaseOpeningPopup →
        // PendingCharacterSelection → CharacterSelectController.
        public string GrantedCharacterId;
        // -1 = pacote sem limite de compras (case de moeda).
        public int RemainingPurchasesForPlayer;
        public List<string> ReelPoolCharacterTypeIds = new List<string>();
    }

    public static async Task<CasePurchaseResult> PurchaseCaseAsync(string packageId, string paymentReceipt = null)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "packageId", packageId } };
            if (!string.IsNullOrEmpty(paymentReceipt)) payload["paymentReceipt"] = paymentReceipt;

            var callable = Functions.GetHttpsCallable("purchaseCase");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[CaseService] Resposta de purchaseCase em formato inesperado (não é um objeto).");
                return new CasePurchaseResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            var result = new CasePurchaseResult
            {
                Success = true,
                WonCharacterTypeId = dict["wonCharacterTypeId"] as string,
                GrantedCharacterId = dict["grantedCharacterId"] as string,
                RemainingPurchasesForPlayer = Convert.ToInt32(dict["remainingPurchasesForPlayer"]),
            };

            if (dict["reelPoolCharacterTypeIds"] is IList poolList)
                foreach (var item in poolList)
                    if (item is string s) result.ReelPoolCharacterTypeIds.Add(s);

            return result;
        }
        catch (FunctionsException e)
        {
            // Bug real corrigido (2026-07-23): este catch nunca logava nada - qualquer erro que
            // chegasse aqui (incluindo a chamada pra região errada, ver FunctionsRegion acima)
            // ficava completamente silencioso no Console, dificultando o diagnóstico ("nenhum log
            // de erro aparece"). "pool esgotada" e "saldo insuficiente" chegam aqui como
            // failed-precondition, distinguidos só pela mensagem (ver purchaseCase.ts) -
            // ShopController decide a UI certa lendo ErrorMessage; o log abaixo é só pra debug,
            // não afeta essa lógica.
            Debug.LogError($"[CaseService] purchaseCase('{packageId}') falhou - código: {e.ErrorCode} ({ToServerErrorCode(e.ErrorCode)}), mensagem: {e.Message}");
            return new CasePurchaseResult { Success = false, ErrorCode = ToServerErrorCode(e.ErrorCode), ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[CaseService] Falha ao comprar case '{packageId}': {e.Message}");
            return new CasePurchaseResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // FunctionsErrorCode.ToString() do SDK C# retorna o nome do membro em PascalCase (ex:
    // "FailedPrecondition") - mas HttpsError do lado da function (purchaseCase.ts) usa a
    // convenção kebab-case padrão do Firebase ("failed-precondition"), a mesma que
    // ShopController.HandleCasePurchaseAsync compara. Mapeamento explícito em vez de confiar em
    // ToString() + manipulação de string, pra não depender de detalhe de implementação do enum.
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
