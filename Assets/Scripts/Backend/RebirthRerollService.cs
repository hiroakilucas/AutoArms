using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `rerollRebirthGrant` (2026-07-26) — reroll individual de um item
// concedido pelo Renascimento (RebirthService.RebirthAsync), MESMO padrão de
// UnlockRerollService/rerollUnlock: custo FIXO (15 diamantes, não escala) e limite de 2 refreshes
// por item, ambos validados/decididos no servidor (functions/src/rerollShared.ts, compartilhado
// com rerollUnlock) — nunca decidido/gasto localmente (ARQUITETURA.md "Moeda premium").
public static class RebirthRerollService
{
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public const int CostDiamonds = 15;
    public const int MaxRerollsPerSlot = 2;

    public class RerollResult
    {
        public bool Success;
        public string ErrorCode;
        public string ErrorMessage;

        // "skill" | "weapon" | "pet" — resolvido pro LevelUpOption real via
        // CharacterUnlockEngine.ResolveServerResult (mesmo helper usado pelo reroll de
        // case-opening, a function só devolve nome+tier).
        public string Kind;
        public string Name;
        public int Tier;
        public int RemainingRerolls;
    }

    public static async Task<RerollResult> RerollAsync(string characterId, int slotIndex)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "characterId", characterId }, { "slotIndex", slotIndex } };
            var callable = Functions.GetHttpsCallable("rerollRebirthGrant");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[RebirthRerollService] Resposta de rerollRebirthGrant em formato inesperado (não é um objeto).");
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
            Debug.LogError($"[RebirthRerollService] rerollRebirthGrant falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");
            return new RerollResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[RebirthRerollService] Falha ao chamar rerollRebirthGrant: {e.Message}");
            return new RerollResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de UnlockRerollService.ToServerErrorCode — FunctionsErrorCode.ToString() do
    // SDK C# devolve PascalCase, mas HttpsError do lado da function usa kebab-case.
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
