using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `rerollLevelUpBoxes` (2026-07-26) — correção de segurança do "Novo
// Sorteio" (CombatResultPanel.cs): custo FIXO (CostDiamonds, mesmo valor de
// rerollUnlock/rerollRebirthGrant) validado/debitado sempre no servidor, nunca mais
// WalletService.SpendDiamondsAsync client-side. Esta function só AUTORIZA o gasto — o sorteio das
// N caixas em si continua client-side (ver comentário em functions/src/rerollLevelUpBoxes.ts pro
// motivo do escopo reduzido).
public static class LevelUpRerollService
{
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public const int CostDiamonds = 15;
    public const int MaxRerollsPerLevelUp = 3;

    public class RerollAuthResult
    {
        public bool Success;
        public string ErrorCode;
        public string ErrorMessage;
        public int RemainingUses;
        public int RemainingDiamonds;
    }

    public static async Task<RerollAuthResult> SpendRerollAsync(string characterId)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "characterId", characterId } };
            var callable = Functions.GetHttpsCallable("rerollLevelUpBoxes");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[LevelUpRerollService] Resposta de rerollLevelUpBoxes em formato inesperado (não é um objeto).");
                return new RerollAuthResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            return new RerollAuthResult
            {
                Success = true,
                RemainingUses = Convert.ToInt32(dict["remainingUses"]),
                RemainingDiamonds = Convert.ToInt32(dict["remainingDiamonds"]),
            };
        }
        catch (FunctionsException e)
        {
            string code = ToServerErrorCode(e.ErrorCode);
            Debug.LogError($"[LevelUpRerollService] rerollLevelUpBoxes('{characterId}') falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");
            return new RerollAuthResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelUpRerollService] Falha ao chamar rerollLevelUpBoxes: {e.Message}");
            return new RerollAuthResult { Success = false, ErrorMessage = e.Message };
        }
    }

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
