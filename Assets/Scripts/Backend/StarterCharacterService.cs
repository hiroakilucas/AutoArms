using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

// Cliente da Cloud Function `grantStarterCharacter` (2026-07-24, onboarding — escolha do 1º
// personagem) — ver functions/src/grantStarterCharacter.ts. Mesmo princípio de CaseService.cs:
// nunca sorteia a skill nem decide o characterTypeId no cliente, só envia a INTENÇÃO ("quero
// este dos 4 templates") e aplica o resultado que o servidor decidiu (personagem + skill inicial
// já vêm prontos).
public static class StarterCharacterService
{
    // Precisa bater exatamente com a região onde grantStarterCharacter está implantada — mesmo
    // motivo/bug documentado em CaseService.cs (FirebaseFunctions.DefaultInstance sem região
    // aponta pra us-central1, onde a function nunca foi implantada).
    private const string FunctionsRegion = "southamerica-east1";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);

    public class StarterGrantResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "invalid-argument", "failed-precondition" (conta já possui personagem), "internal".
        public string ErrorCode;
        public string ErrorMessage;

        public string CharacterTypeId;
        // ID do documento concedido em users/{uid}/characters — usado pro handoff
        // ChooseFirstCharacterController → PendingCharacterSelection → CharacterSelectController
        // (mesmo canal que o case opening já usa).
        public string GrantedCharacterId;
        public string GrantedSkillName;
    }

    public static async Task<StarterGrantResult> GrantStarterCharacterAsync(string characterTypeId)
    {
        try
        {
            var payload = new Dictionary<string, object> { { "characterTypeId", characterTypeId } };

            var callable = Functions.GetHttpsCallable("grantStarterCharacter");
            var response = await callable.CallAsync(payload);

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError("[StarterCharacterService] Resposta de grantStarterCharacter em formato inesperado (não é um objeto).");
                return new StarterGrantResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            return new StarterGrantResult
            {
                Success = true,
                CharacterTypeId = dict["characterTypeId"] as string,
                GrantedCharacterId = dict["grantedCharacterId"] as string,
                GrantedSkillName = dict["grantedSkillName"] as string,
            };
        }
        catch (FunctionsException e)
        {
            Debug.LogError($"[StarterCharacterService] grantStarterCharacter('{characterTypeId}') falhou - código: {e.ErrorCode} ({ToServerErrorCode(e.ErrorCode)}), mensagem: {e.Message}");
            return new StarterGrantResult { Success = false, ErrorCode = ToServerErrorCode(e.ErrorCode), ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[StarterCharacterService] Falha ao conceder personagem inicial '{characterTypeId}': {e.Message}");
            return new StarterGrantResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de CaseService.cs (FunctionsErrorCode.ToString() é PascalCase; HttpsError
    // do lado da function usa kebab-case) — duplicado em vez de compartilhado porque não existe
    // hoje nenhum utilitário comum entre os serviços de Cloud Function deste projeto.
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
