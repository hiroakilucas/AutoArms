using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Energia por personagem (users/{uid}/characters/{characterId}, campos energyCurrent/
// lastEnergyTimestamp) — 2026-07-19, pedido do usuário: 10 batalhas diárias, +1 a cada 2h até o
// teto, NUNCA usando o relógio local do device pra decidir quanto tempo passou (senão adiantar a
// hora do celular vira trapaça).
//
// Como conseguir um "agora" confiável sem Cloud Function: não existe uma leitura direta de "hora
// do servidor" no SDK client-side, mas dá pra obter uma indiretamente — grava um campo qualquer
// com FieldValue.ServerTimestamp (o próprio Firestore resolve o valor no servidor no momento da
// escrita) e lê de volta forçando Source.Server (ignora cache local). Isso fecha o mesmo buraco de
// segurança (o valor não vem do device) sem precisar de infraestrutura de Functions ainda — ver
// ARQUITETURA.md/WalletService.cs pro motivo de diamante seguir um caminho diferente (esse
// SIM precisa de Cloud Function, por envolver saldo gasto, não só leitura de tempo).
public static class EnergyService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;
    private const string ServerTimeProbeField = "_energyServerTimeProbe";

    private static DocumentReference CharacterDoc(string uid, string characterId) =>
        Db.Collection("users").Document(uid).Collection("characters").Document(characterId);

    // Lê energyCurrent/lastEnergyTimestamp, aplica a regeneração pendente (se houver) e devolve o
    // valor já atualizado — grava de volta no Firestore só quando algo de fato mudou. Primeira vez
    // que o personagem é lido (documento sem os dois campos ainda) inicializa cheio.
    public static async Task<(int current, int max)> GetOrRegenAsync(string uid, string characterId, EnergySettings settings)
    {
        var doc = CharacterDoc(uid, characterId);
        // Sempre atualizado, não importa o ramo abaixo — PlayerEconomyState.FormatEnergyCountdown
        // (timer de "próxima energia", 2026-07-20) precisa do valor configurado mesmo quando o
        // resto da função retorna cedo.
        PlayerEconomyState.RegenIntervalHours = settings.regenIntervalHours;
        try
        {
            FirestoreService.TryEnsurePersistence();
            DocumentSnapshot snap = await doc.GetSnapshotAsync();

            // Pré-declarados com default (não `out var` inline) — o compilador não consegue provar
            // definite assignment de storedCurrent/storedTimestamp só pelo && curto-circuitar em
            // snap.Exists, mesmo os dois só sendo lidos depois do guard hasCurrent/hasTimestamp
            // abaixo confirmar sucesso (CS0165).
            long storedCurrent = 0;
            Timestamp storedTimestamp = default;
            bool hasCurrent = snap.Exists && snap.TryGetValue("energyCurrent", out storedCurrent);
            bool hasTimestamp = snap.Exists && snap.TryGetValue("lastEnergyTimestamp", out storedTimestamp);

            if (!hasCurrent || !hasTimestamp)
            {
                await doc.SetAsync(new Dictionary<string, object>
                {
                    { "energyCurrent", settings.maxEnergy },
                    { "lastEnergyTimestamp", FieldValue.ServerTimestamp },
                }, SetOptions.MergeAll);
                PlayerEconomyState.EnergyCurrent = settings.maxEnergy;
                PlayerEconomyState.EnergyMax = settings.maxEnergy;
                // Doc recém-criado — lastEnergyTimestamp é FieldValue.ServerTimestamp (sentinela,
                // sem valor resolvido em mãos sem outro round-trip); "agora" é uma aproximação
                // aceitável, já que EnergyCurrent==Max esconde o timer de qualquer forma
                // (FormatEnergyCountdown mostra "Energia cheia" nesse caso, não usa o timestamp).
                PlayerEconomyState.LastEnergyTimestampUtc = DateTime.UtcNow;
                return (settings.maxEnergy, settings.maxEnergy);
            }

            int current = (int)storedCurrent;
            if (current >= settings.maxEnergy)
            {
                PlayerEconomyState.EnergyCurrent = current;
                PlayerEconomyState.EnergyMax = settings.maxEnergy;
                PlayerEconomyState.LastEnergyTimestampUtc = storedTimestamp.ToDateTime();
                return (current, settings.maxEnergy);
            }

            DateTime serverNow = await ReadServerNowAsync(doc);
            DateTime last = storedTimestamp.ToDateTime();
            double elapsedHours = (serverNow - last).TotalHours;
            int intervals = (int)Math.Floor(elapsedHours / settings.regenIntervalHours);

            if (intervals <= 0)
            {
                PlayerEconomyState.EnergyCurrent = current;
                PlayerEconomyState.EnergyMax = settings.maxEnergy;
                PlayerEconomyState.LastEnergyTimestampUtc = last;
                return (current, settings.maxEnergy);
            }

            int newCurrent = Mathf.Min(settings.maxEnergy, current + intervals);
            // Cheio (bateu ou passou do teto): reancora o relógio em "agora" — não sobra
            // progresso parcial pra preservar, e sem isso um personagem parado por muito tempo
            // no teto (ex: 20h cheio) reencheria instantaneamente de novo assim que gastasse 1,
            // porque o timestamp antigo geraria "horas suficientes" pra pular direto de volta ao
            // teto no primeiro consumo seguinte. Só quando fica ABAIXO do teto é que avançamos
            // pelo tempo EXATO consumido (intervals * regenIntervalHours), preservando o
            // progresso parcial rumo ao próximo tick, como pedido.
            DateTime newTimestamp = newCurrent >= settings.maxEnergy
                ? serverNow
                : last.AddHours(intervals * settings.regenIntervalHours);

            await doc.UpdateAsync(new Dictionary<string, object>
            {
                { "energyCurrent", newCurrent },
                { "lastEnergyTimestamp", Timestamp.FromDateTime(DateTime.SpecifyKind(newTimestamp, DateTimeKind.Utc)) },
            });

            PlayerEconomyState.EnergyCurrent = newCurrent;
            PlayerEconomyState.EnergyMax = settings.maxEnergy;
            PlayerEconomyState.LastEnergyTimestampUtc = newTimestamp;
            return (newCurrent, settings.maxEnergy);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyService] Falha ao ler/regenerar energia de '{characterId}': {e.Message}");
            return (PlayerEconomyState.EnergyCurrent, settings.maxEnergy);
        }
    }

    // Extraído pra FirestoreService.ReadServerNowAsync (2026-07-27) — mesma implementação (grava
    // um campo descartável com FieldValue.ServerTimestamp, lê de volta forçando Source.Server,
    // até 3 tentativas), generalizada pra qualquer documento/campo depois que DailyRewardsService
    // precisou do MESMO truque pros resgates de diamante. Este wrapper só existe pra não precisar
    // mudar as 3 call sites já existentes abaixo.
    private static Task<DateTime> ReadServerNowAsync(DocumentReference doc) =>
        FirestoreService.ReadServerNowAsync(doc, ServerTimeProbeField, "EnergyService");

    // Consome 1 energia (chamado só depois de GetOrRegenAsync confirmar current > 0) — NÃO mexe
    // em lastEnergyTimestamp: consumir energia não deve resetar/adiantar o relógio de
    // regeneração, só o valor atual muda.
    public static async Task ConsumeOneAsync(string uid, string characterId, int currentAfterRegen)
    {
        int newValue = Mathf.Max(0, currentAfterRegen - 1);
        try
        {
            FirestoreService.TryEnsurePersistence();
            await CharacterDoc(uid, characterId).UpdateAsync(new Dictionary<string, object> { { "energyCurrent", newValue } });
            PlayerEconomyState.EnergyCurrent = newValue;
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyService] Falha ao consumir energia de '{characterId}': {e.Message}");
        }
    }

    // Preço progressivo de "pagar pra continuar jogando hoje" (2026-07-21, pedido do usuário,
    // substitui o antigo custo fixo único `EnergySettings.diamondCostToRefill`) — POR PERSONAGEM
    // (campos em users/{uid}/characters/{characterId}, mesmo documento de energyCurrent/
    // lastEnergyTimestamp — pagar no Personagem A não afeta o contador do Personagem B), reseta à
    // meia-noite em hora do SERVIDOR (nunca o relógio do device — mesmo padrão de
    // ReadServerNowAsync já usado pela regeneração natural, reaproveitado aqui em vez de criar
    // uma fonte de tempo paralela). Campos novos, independentes de energyCurrent/
    // lastEnergyTimestamp (a regeneração natural de 2h em 2h não muda em nada com isto):
    //   energyRefillPaymentsToday (int) — quantas vezes já pagou HOJE pra este personagem.
    //   energyRefillLastPaymentTimestamp (Timestamp) — quando foi o último pagamento; se a DATA
    //   (em UTC, resolvida no servidor) for diferente de "agora", o contador é tratado como
    //   zerado (sem precisar de um job/Cloud Function rodando à meia-noite pra "resetar" nada —
    //   o reset é só "ignorar o valor salvo se for de um dia anterior").
    private static int ReadPaymentsToday(DocumentSnapshot snap, DateTime serverNow)
    {
        if (snap.Exists
            && snap.TryGetValue("energyRefillPaymentsToday", out long storedCount)
            && snap.TryGetValue("energyRefillLastPaymentTimestamp", out Timestamp storedTs)
            && storedTs.ToDateTime().Date == serverNow.Date)
        {
            return (int)storedCount;
        }
        return 0; // nunca pagou hoje (ou o último pagamento foi num dia anterior)
    }

    // 1ª vez no dia = refillCostTier1, 2ª = refillCostTier2, 3ª em diante = refillCostTier3Plus
    // (travado, não continua dobrando).
    private static int RefillCostForPaymentIndex(int paymentsToday, EnergySettings settings) => paymentsToday switch
    {
        0 => settings.refillCostTier1,
        1 => settings.refillCostTier2,
        _ => settings.refillCostTier3Plus,
    };

    // Só CALCULA o custo da próxima vez que o jogador pagaria hoje pra este personagem — não
    // gasta diamante nem altera nada, usado pra mostrar o valor certo no popup de confirmação
    // ANTES do jogador decidir (ver MainMenuController.ShowRefillConfirmPopup).
    public static async Task<int> GetRefillCostAsync(string uid, string characterId, EnergySettings settings)
    {
        var doc = CharacterDoc(uid, characterId);
        try
        {
            FirestoreService.TryEnsurePersistence();
            DateTime serverNow = await ReadServerNowAsync(doc);
            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            int paymentsToday = ReadPaymentsToday(snap, serverNow);
            return RefillCostForPaymentIndex(paymentsToday, settings);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyService] Falha ao calcular custo de continuar jogando '{characterId}': {e.Message}");
            return settings.refillCostTier1; // fallback conservador (o mais barato) — nunca cobra mais do que deveria por causa de um erro de leitura
        }
    }

    // Gasta o diamante (WalletService.SpendDiamondsAsync — mesma regra de segurança/placeholder
    // de sempre, ver ARQUITETURA.md "Moeda premium"), incrementa o contador de pagamentos de HOJE
    // e reabastece 1 energia — tudo só depois do gasto confirmar (se SpendDiamondsAsync falhar,
    // nada mais muda: nem o contador, nem a energia). Recalcula o custo de novo aqui (mesma lógica
    // de GetRefillCostAsync) em vez de confiar num valor já mostrado na tela — evita cobrar um
    // preço desatualizado se o dia virou ou outro pagamento aconteceu entre abrir o popup e
    // confirmar. Também não mexe em lastEnergyTimestamp (pagar não deveria dar de graça um
    // "avanço" no relógio de regeneração natural, mesmo espírito do antigo RefillOneAsync).
    public static async Task<(bool success, int cost)> PayToRefillAsync(string uid, string characterId, EnergySettings settings)
    {
        var doc = CharacterDoc(uid, characterId);
        try
        {
            FirestoreService.TryEnsurePersistence();
            DateTime serverNow = await ReadServerNowAsync(doc);
            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            int paymentsToday = ReadPaymentsToday(snap, serverNow);
            int cost = RefillCostForPaymentIndex(paymentsToday, settings);

            bool spent = await WalletService.SpendDiamondsAsync(uid, cost);
            if (!spent) return (false, cost);

            await doc.UpdateAsync(new Dictionary<string, object>
            {
                { "energyRefillPaymentsToday", paymentsToday + 1 },
                { "energyRefillLastPaymentTimestamp", Timestamp.FromDateTime(DateTime.SpecifyKind(serverNow, DateTimeKind.Utc)) },
                { "energyCurrent", 1 },
            });
            PlayerEconomyState.EnergyCurrent = 1;
            return (true, cost);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnergyService] Falha ao pagar pra continuar jogando '{characterId}': {e.Message}");
            return (false, 0);
        }
    }
}
