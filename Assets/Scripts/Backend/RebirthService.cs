using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `rebirthCharacter` (2026-07-26) — feature "Renascimento" (Reset
// Nível 10+), separada do "Resetar Personagem" antigo (grátis, sem gate, 100% client-side, ver
// CharacterPanel.ExecuteReset). Nunca decide nível/sorteio localmente — só envia a INTENÇÃO
// (characterId) e aplica o que o servidor já persistiu (level, stats, itens concedidos, moeda
// creditada), mesmo princípio de segurança de CaseService/UnlockRerollService (ARQUITETURA.md
// "Moeda premium" — vale igual pra moeda comum aqui, nunca creditada pelo cliente).
//
// ECONOMIA INVERTIDA (correção de escopo, 2026-07-26, mesmo dia) — Renascimento é 100% GRATUITO
// (sem custo em Coins/Diamantes); a versão original desta function DEBITAVA moeda e podia falhar
// com "failed-precondition"/"Saldo de moedas insuficiente" (removido, não existe mais).
public static class RebirthService
{
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public class GrantedItem
    {
        public string Kind; // "skill" | "weapon" | "pet"
        public string Name;
        public int Tier;
    }

    public class RebirthResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "invalid-argument", "not-found", "failed-precondition" (level < 10 — não mais saldo,
        // ver ErrorMessage), "internal".
        public string ErrorCode;
        public string ErrorMessage;

        public int Level;
        public int MaxHealth;
        public int Str;
        public int Agility;
        public int Speed;
        public int CoinsRewarded;
        public int NewCoinsBalance;
        public List<GrantedItem> GrantedItems = new List<GrantedItem>();
    }

    public static async Task<RebirthResult> RebirthAsync(string characterId)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "characterId", characterId } };
            var callable = Functions.GetHttpsCallable("rebirthCharacter");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[RebirthService] Resposta de rebirthCharacter em formato inesperado (não é um objeto).");
                return new RebirthResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            var result = new RebirthResult
            {
                Success = true,
                Level = Convert.ToInt32(dict["level"]),
                MaxHealth = Convert.ToInt32(dict["maxHealth"]),
                Str = Convert.ToInt32(dict["str"]),
                Agility = Convert.ToInt32(dict["agility"]),
                Speed = Convert.ToInt32(dict["speed"]),
                CoinsRewarded = Convert.ToInt32(dict["coinsRewarded"]),
                NewCoinsBalance = Convert.ToInt32(dict["newCoinsBalance"]),
            };

            if (dict["grantedItems"] is IList itemsList)
                foreach (var raw in itemsList)
                    if (raw is IDictionary itemDict)
                        result.GrantedItems.Add(new GrantedItem
                        {
                            Kind = itemDict["kind"] as string,
                            Name = itemDict["name"] as string,
                            Tier = Convert.ToInt32(itemDict["tier"]),
                        });

            return result;
        }
        catch (FunctionsException e)
        {
            string code = ToServerErrorCode(e.ErrorCode);
            Debug.LogError($"[RebirthService] rebirthCharacter('{characterId}') falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");
            return new RebirthResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[RebirthService] Falha ao renascer personagem '{characterId}': {e.Message}");
            return new RebirthResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de CaseService.ToServerErrorCode/UnlockRerollService.ToServerErrorCode.
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
