using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `rerollUnlock` (2026-07-25, refresh dos unlocks progressivos de
// skill/arma/pet concedidos ao ganhar personagem via case opening - ver CharacterUnlockEngine/
// CharacterSelectController.ResolveCaseUnlocksAsync). Mesmo princípio de CaseService/
// purchaseCase: nunca decrementa diamante direto no client, nunca decide o resultado do
// resorteio localmente - só envia a INTENÇÃO (qual personagem, qual unlock) e aplica o que o
// servidor decidiu (ARQUITETURA.md "Moeda premium (diamantes) — regra de segurança
// inegociável"). Custo FIXO (15 diamantes, não escala) e limite de 2 refreshes por unlock são
// ambos validados/decididos no servidor, nunca só no client.
public static class UnlockRerollService
{
    // Mesma região de purchaseCase.ts (functions/src/rerollUnlock.ts) — FirebaseFunctions.
    // DefaultInstance aponta pra us-central1 por padrão, onde a function nunca foi implantada.
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public const int CostDiamonds = 15;
    public const int MaxRerollsPerUnlock = 2;

    public class RerollResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "invalid-argument", "not-found", "failed-precondition" (saldo insuficiente),
        // "resource-exhausted" (limite de refresh atingido), "internal".
        public string ErrorCode;
        public string ErrorMessage;

        // "skill" | "weapon" | "pet" — resolvido pro LevelUpOption de verdade via
        // CharacterUnlockEngine.ResolveServerResult (precisa dos databases locais pra achar o
        // SkillData/WeaponData/PetData real, a function só devolve nome+tier).
        public string Kind;
        public string Name;
        public int Tier;
        public int RemainingRerolls;
    }

    public static async Task<RerollResult> RerollUnlockAsync(string characterId, int unlockIndex)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "characterId", characterId }, { "unlockIndex", unlockIndex } };
            var callable = Functions.GetHttpsCallable("rerollUnlock");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[UnlockRerollService] Resposta de rerollUnlock em formato inesperado (não é um objeto).");
                return new RerollResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            return new RerollResult
            {
                Success = true,
                Kind = dict["kind"] as string,
                Name = dict["name"] as string,
                Tier = Convert.ToInt32(dict["tier"]),
                RemainingRerolls = Convert.ToInt32(dict["remainingRerolls"]),
            };
        }
        catch (FunctionsException e)
        {
            string code = ToServerErrorCode(e.ErrorCode);
            Debug.LogError($"[UnlockRerollService] rerollUnlock falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");
            return new RerollResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnlockRerollService] Falha ao chamar rerollUnlock: {e.Message}");
            return new RerollResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de CaseService.ToServerErrorCode — FunctionsErrorCode.ToString() do SDK
    // C# devolve PascalCase, mas HttpsError do lado da function usa kebab-case.
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
