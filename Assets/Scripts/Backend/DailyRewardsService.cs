using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Functions;
using UnityEngine;

// Resgates gratuitos de diamante — Diário/Semanal/Mensal (2026-07-27, pedido do usuário, aba
// Diamantes da Loja). Mesma regra inegociável de "moeda premium nunca client-writable"
// (ARQUITETURA.md "Moeda premium") — o crédito em si SEMPRE passa pelas Cloud Functions
// claimDailyDiamonds/claimWeeklyDiamonds/claimMonthlyDiamonds (functions/src/
// dailyDiamondRewards.ts), nunca WalletService.AddDiamondsAsync (esse é o caminho client-writable
// de outros fluxos, TODO de segurança pré-existente — não usado aqui de propósito).
//
// Fuso horário: America/Sao_Paulo, fixo em UTC-3 (Brasil não observa horário de verão desde
// 2019) — mesma regra de período usada no servidor (que calcula via Intl.DateTimeFormat com o
// timeZone IANA, mais robusto a uma eventual reintrodução do horário de verão). Este espelho
// client-side existe só pra DECIDIR O QUE MOSTRAR (botão habilitado/contagem regressiva) — a
// decisão de crédito de verdade sempre revalida no servidor; se os dois cálculos algum dia
// divergirem (só aconteceria se o Brasil reintroduzisse horário de verão), o pior caso é um botão
// mostrando o estado errado por alguns instantes até a próxima sincronização, nunca uma
// recompensa duplicada (a Cloud Function decide contra o próprio relógio dela, não confia neste
// cálculo local em nenhum momento).
public static class DailyRewardsService
{
    public enum RewardType { Daily, Weekly, Monthly }

    private const string FunctionsRegion = "southamerica-east1";
    private const string ServerTimeProbeField = "_dailyRewardsServerTimeProbe";

    private static FirebaseFunctions Functions => FirebaseFunctions.GetInstance(Firebase.FirebaseApp.DefaultInstance, FunctionsRegion);
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static DocumentReference RewardsStateDoc(string uid) =>
        Db.Collection("users").Document(uid).Collection("rewardsState").Document("diamonds");

    // Doc PRINCIPAL da conta (mesmo `users/{uid}` de WalletService/NextCharacterService) — só
    // usado aqui como alvo do "probe" de hora do servidor (ver ReadServerNowAsync). Precisa ser
    // um documento CLIENT-WRITABLE pro truque funcionar (grava um campo descartável e lê de
    // volta) — `rewardsState/diamonds` de propósito NÃO é (allow write: if false, ver
    // firestore.rules, é o que protege os campos de período de serem forjados pelo cliente), então
    // não pode ser o alvo do probe. `users/{uid}` já é lido/escrito pelo cliente em vários outros
    // fluxos (coins/diamonds), então usar ele aqui não abre nenhuma superfície nova.
    private static DocumentReference UserDoc(string uid) => Db.Collection("users").Document(uid);

    // Mesmo offset usado no comentário de topo — ver ali pro porquê de ser fixo em vez de uma
    // conversão de fuso horário "de verdade" (TimeZoneInfo teria IDs diferentes entre plataformas
    // pro mesmo fuso IANA, um problema conhecido do Unity/mobile; um offset fixo evita essa
    // divergência multiplataforma sem custo, já que Brasil não observa DST hoje).
    private static readonly TimeSpan SaoPauloOffset = TimeSpan.FromHours(-3);

    private static DateTime ToSaoPaulo(DateTime utc) => DateTime.SpecifyKind(utc + SaoPauloOffset, DateTimeKind.Unspecified);
    private static DateTime SaoPauloToUtc(DateTime sp) => DateTime.SpecifyKind(sp - SaoPauloOffset, DateTimeKind.Utc);

    private static string DailyPeriodKey(DateTime sp) => sp.ToString("yyyy-MM-dd");
    private static DateTime NextDailyResetUtc(DateTime sp) => SaoPauloToUtc(sp.Date.AddDays(1));

    private static string MonthlyPeriodKey(DateTime sp) => sp.ToString("yyyy-MM");
    private static DateTime NextMonthlyResetUtc(DateTime sp) => SaoPauloToUtc(new DateTime(sp.Year, sp.Month, 1).AddMonths(1));

    // Segunda-feira (00:00, calendário SP) que iniciou a semana de `sp` — mesma fórmula do lado
    // servidor (dailyDiamondRewards.ts, weeklyPeriodKey): DayOfWeek é Sunday=0..Saturday=6;
    // `(dow + 6) % 7` converte pra "dias desde a última segunda" (segunda=0, domingo=6).
    private static DateTime WeekMonday(DateTime sp)
    {
        int daysSinceMonday = ((int)sp.DayOfWeek + 6) % 7;
        return sp.Date.AddDays(-daysSinceMonday);
    }
    private static string WeeklyPeriodKey(DateTime sp) => WeekMonday(sp).ToString("yyyy-MM-dd");
    private static DateTime NextWeeklyResetUtc(DateTime sp) => SaoPauloToUtc(WeekMonday(sp).AddDays(7));

    private static string FieldNameFor(RewardType type) => type switch
    {
        RewardType.Daily => "dailyLastPeriod",
        RewardType.Weekly => "weeklyLastPeriod",
        _ => "monthlyLastPeriod",
    };

    private static string FunctionNameFor(RewardType type) => type switch
    {
        RewardType.Daily => "claimDailyDiamonds",
        RewardType.Weekly => "claimWeeklyDiamonds",
        _ => "claimMonthlyDiamonds",
    };

    // Espelha DAILY_CONFIG/WEEKLY_CONFIG/MONTHLY_CONFIG de dailyDiamondRewards.ts — só pra
    // EXIBIÇÃO (o valor de fato creditado sempre vem da resposta da Cloud Function).
    public static int AmountFor(RewardType type) => type switch
    {
        RewardType.Daily => 5,
        RewardType.Weekly => 30,
        _ => 100,
    };

    public static bool IsAvailable(RewardType type) => type switch
    {
        RewardType.Daily => PlayerEconomyState.DailyDiamondsAvailable,
        RewardType.Weekly => PlayerEconomyState.WeeklyDiamondsAvailable,
        _ => PlayerEconomyState.MonthlyDiamondsAvailable,
    };

    public static DateTime NextResetUtcFor(RewardType type) => type switch
    {
        RewardType.Daily => PlayerEconomyState.DailyDiamondsNextResetUtc,
        RewardType.Weekly => PlayerEconomyState.WeeklyDiamondsNextResetUtc,
        _ => PlayerEconomyState.MonthlyDiamondsNextResetUtc,
    };

    // Bolinha vermelha agregada (2026-07-27) — true se QUALQUER um dos 3 resgates estiver
    // disponível agora. Lida do MESMO estado já sincronizado em PlayerEconomyState por
    // RefreshStatusAsync — nunca recalculada de forma independente (pedido explícito do usuário:
    // "ler o saldo real, não duplicar/recalcular de forma independente"). Reaproveitada pelo botão
    // "LOJA" do Main Menu (agregada com NextCharacterService.CanAffordNextPurchase — ver
    // MainMenuController) e pela aba DIAMANTES da Loja.
    public static bool AnyClaimAvailable =>
        PlayerEconomyState.DailyDiamondsAvailable || PlayerEconomyState.WeeklyDiamondsAvailable || PlayerEconomyState.MonthlyDiamondsAvailable;

    // Sincroniza PlayerEconomyState a partir do Firestore — mesmo espírito de EnergyService.
    // GetOrRegenAsync: nunca usa o relógio local pra decidir disponibilidade, só pra fins de
    // exibição entre uma sincronização e outra (FirestoreService.ReadServerNowAsync sempre lê a
    // hora real do servidor primeiro). Chamado toda vez que a economia é recarregada (Loja e Main
    // Menu) — ver ShopController.LoadPersistedShopStateAsync/MainMenuController.
    // RefreshEconomyOnMenuLoad.
    public static async Task RefreshStatusAsync(string uid)
    {
        var doc = RewardsStateDoc(uid);
        try
        {
            FirestoreService.TryEnsurePersistence();
            // Bug real corrigido (2026-07-27) — o probe original escrevia direto em `doc`
            // (rewardsState/diamonds), mas esse documento tem `allow write: if false` (de
            // propósito, ver comentário em UserDoc acima) — toda chamada falhava com "Missing or
            // insufficient permissions" antes mesmo de chegar na leitura de período. O probe
            // precisa mirar um documento que o cliente PODE escrever; `UserDoc(uid)` já é.
            DateTime serverNowUtc = await FirestoreService.ReadServerNowAsync(UserDoc(uid), ServerTimeProbeField, "DailyRewardsService");
            DateTime sp = ToSaoPaulo(serverNowUtc);

            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            string dailyStored = TryGetString(snap, "dailyLastPeriod");
            string weeklyStored = TryGetString(snap, "weeklyLastPeriod");
            string monthlyStored = TryGetString(snap, "monthlyLastPeriod");

            PlayerEconomyState.DailyDiamondsAvailable = dailyStored != DailyPeriodKey(sp);
            PlayerEconomyState.WeeklyDiamondsAvailable = weeklyStored != WeeklyPeriodKey(sp);
            PlayerEconomyState.MonthlyDiamondsAvailable = monthlyStored != MonthlyPeriodKey(sp);

            PlayerEconomyState.DailyDiamondsNextResetUtc = NextDailyResetUtc(sp);
            PlayerEconomyState.WeeklyDiamondsNextResetUtc = NextWeeklyResetUtc(sp);
            PlayerEconomyState.MonthlyDiamondsNextResetUtc = NextMonthlyResetUtc(sp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DailyRewardsService] Falha ao sincronizar estado dos resgates: {e.Message}");
        }
    }

    private static string TryGetString(DocumentSnapshot snap, string field) =>
        snap.Exists && snap.TryGetValue<string>(field, out var v) ? v : null;

    public class ClaimResult
    {
        public bool Success;
        // Códigos possíveis (espelham HttpsError do lado da function): "unauthenticated",
        // "failed-precondition" (já resgatado neste período — pode ter mudado entre abrir a tela
        // e clicar, ex: resgatou em outro device), "internal".
        public string ErrorCode;
        public string ErrorMessage;
        public int DiamondsGranted;
        public int NewDiamondsBalance;
    }

    // Chama a Cloud Function (nível/janela/sorteio 100% server-side, nunca decidido aqui) e aplica
    // o saldo já persistido — mesmo princípio de segurança de NextCharacterService/RebirthService.
    // Em sucesso, atualiza PlayerEconomyState.Diamonds + marca este tipo como indisponível até o
    // próximo período (estimado a partir de DateTime.UtcNow — só decorativo, corrigido de verdade
    // na próxima RefreshStatusAsync; nunca usado pra decidir crédito). Em falha por
    // "failed-precondition" (já resgatado — corrida entre devices/abas), resincroniza o estado
    // antes de devolver, pra a UI não ficar mostrando "disponível" pra um resgate que não é mais.
    public static async Task<ClaimResult> ClaimAsync(RewardType type)
    {
        try
        {
            var callable = Functions.GetHttpsCallable(FunctionNameFor(type));
            var response = await callable.CallAsync(new Dictionary<string, object>());

            var dict = response.Data as IDictionary;
            if (dict == null)
            {
                Debug.LogError($"[DailyRewardsService] Resposta de {FunctionNameFor(type)} em formato inesperado (não é um objeto).");
                return new ClaimResult { Success = false, ErrorMessage = "Resposta inesperada do servidor." };
            }

            int granted = Convert.ToInt32(dict["diamondsGranted"]);
            int newBalance = Convert.ToInt32(dict["newDiamondsBalance"]);
            PlayerEconomyState.Diamonds = newBalance;

            DateTime nowSp = ToSaoPaulo(DateTime.UtcNow);
            switch (type)
            {
                case RewardType.Daily:
                    PlayerEconomyState.DailyDiamondsAvailable = false;
                    PlayerEconomyState.DailyDiamondsNextResetUtc = NextDailyResetUtc(nowSp);
                    break;
                case RewardType.Weekly:
                    PlayerEconomyState.WeeklyDiamondsAvailable = false;
                    PlayerEconomyState.WeeklyDiamondsNextResetUtc = NextWeeklyResetUtc(nowSp);
                    break;
                default:
                    PlayerEconomyState.MonthlyDiamondsAvailable = false;
                    PlayerEconomyState.MonthlyDiamondsNextResetUtc = NextMonthlyResetUtc(nowSp);
                    break;
            }

            return new ClaimResult { Success = true, DiamondsGranted = granted, NewDiamondsBalance = newBalance };
        }
        catch (FunctionsException e)
        {
            string code = ToServerErrorCode(e.ErrorCode);
            Debug.LogError($"[DailyRewardsService] {FunctionNameFor(type)} falhou - código: {e.ErrorCode} ({code}), mensagem: {e.Message}");

            if (code == "failed-precondition" && AuthService.IsSignedIn)
                await RefreshStatusAsync(AuthService.CurrentUser.UserId);

            return new ClaimResult { Success = false, ErrorCode = code, ErrorMessage = e.Message };
        }
        catch (Exception e)
        {
            Debug.LogError($"[DailyRewardsService] Falha ao resgatar {FunctionNameFor(type)}: {e.Message}");
            return new ClaimResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // Mesmo mapeamento de CaseService/NextCharacterService.ToServerErrorCode.
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
