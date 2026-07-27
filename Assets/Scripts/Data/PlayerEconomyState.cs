using System;
using System.Collections.Generic;

// Canal cross-scene só pra exibição de moeda/diamante/energia — mesmo espírito de
// ReplayPlaybackState: campo estático puro (não ScriptableObject), porque não é um dado editável
// no Inspector, é cache em memória de sessão populado pelo WalletService/EnergyService depois de
// cada leitura/gravação no Firestore. CharacterPanel/MainMenuCharacterPreview leem os campos aqui
// diretamente (síncrono, sem esperar Task nenhuma) pra desenhar o HUD; quem gravou no Firestore é
// responsável por atualizar estes campos logo em seguida (WalletService/EnergyService já fazem
// isso nos próprios métodos).
public static class PlayerEconomyState
{
    public static int Coins;
    public static int Diamonds;
    public static int EnergyCurrent;
    public static int EnergyMax = 10;

    // Âncora do relógio de regeneração (2026-07-20, pedido do usuário — timer de "quando vai
    // recuperar") — mesmo valor que EnergyService já usa internamente pro cálculo real de
    // intervalos, só espelhado aqui pra UI conseguir mostrar uma contagem regressiva sem precisar
    // reconsultar o Firestore a cada segundo. `EnergyService.GetOrRegenAsync` mantém isto
    // sincronizado a cada chamada; entre chamadas, o countdown na UI avança usando o relógio
    // LOCAL do device (`DateTime.UtcNow` em `FormatEnergyCountdown`) — isso é só decorativo, o
    // valor de fato gasto/creditado sempre é revalidado contra o servidor em
    // GetOrRegenAsync/ConsumeOneAsync, então adiantar o relógio do device não abre brecha nenhuma
    // nova, só deixaria o texto do timer errado até a próxima sincronização real.
    public static DateTime LastEnergyTimestampUtc;
    public static float RegenIntervalHours = 2f;

    // Falso até a 1ª leitura bem-sucedida da sessão (login ou "Pular offline") — HUDs podem usar
    // isto pra decidir se já têm um número de verdade pra mostrar ou se ainda estão no valor
    // default de fábrica (zerado).
    public static bool IsLoaded;

    // Pacotes de diamante (aba Diamantes da Loja) cujo bônus de 1ª compra já foi usado NESTA
    // conta — 2026-07-21, persistência real (ver ShopStateService), chave = ShopItem.
    // DiamondNormalAmount (30/80/170/360/950/2000/4100/6100). Populado por ShopStateService.
    // LoadAsync ao abrir a Loja; ShopController.BuildItemData lê isto pra decidir se cada card já
    // deve nascer sem o selo "1ª compra: N diamantes".
    public static readonly HashSet<int> UsedFirstPurchaseBonusAmounts = new HashSet<int>();

    // "Próximo Personagem" (Coins, aba Personagens da Loja, 2026-07-26) — quantas vezes esta
    // conta já comprou via `purchaseNextCharacter` (mesmo campo persistido em
    // users/{uid}.nextCharacterPurchaseCount, contador server-authoritative). Populado por
    // ShopController.LoadPersistedShopStateAsync (WalletService.LoadNextCharacterPurchaseCountAsync)
    // antes de montar os cards, pra mostrar o preço real da PRÓXIMA compra em vez de sempre "1ª
    // compra" — ao contrário do antigo contador em memória (ShopItem.Purchased), que resetava a
    // cada carregamento de cena.
    public static int NextCharacterPurchaseCount;

    // Resgates gratuitos de diamante (Diário/Semanal/Mensal, aba Diamantes da Loja, 2026-07-27) —
    // espelham `users/{uid}/rewardsState/diamonds` (populado por `DailyRewardsService.
    // RefreshStatusAsync`, mesmo espírito de `NextCharacterPurchaseCount`/campos de energia acima:
    // cache em memória de sessão, nunca a fonte de verdade — a decisão real de crédito é sempre
    // revalidada no servidor via Cloud Function no momento do resgate). `*NextResetUtc` alimenta a
    // contagem regressiva local (`FormatCountdownUntil` abaixo) entre uma sincronização e outra.
    public static bool DailyDiamondsAvailable;
    public static DateTime DailyDiamondsNextResetUtc;
    public static bool WeeklyDiamondsAvailable;
    public static DateTime WeeklyDiamondsNextResetUtc;
    public static bool MonthlyDiamondsAvailable;
    public static DateTime MonthlyDiamondsNextResetUtc;

    // Formatador genérico de contagem regressiva (2026-07-27, extraído do espírito de
    // FormatEnergyCountdown pra não depender de campos específicos de energia) — usado pelos 3
    // timers de resgate de diamante (Semanal/Mensal passam a maior parte do tempo com dias de
    // sobra; H:MM:SS direto ficava ilegível tipo "144:00:00"). "Disponível agora" quando o alvo
    // já passou (em vez de deixar o timer virar negativo/travar em 0:00:00 — aqui não existe um
    // "onDone" equivalente ao de energia, o botão já reabilita sozinho na próxima
    // RefreshStatusAsync, mas o texto não deve parecer travado até lá).
    //
    // >= 1 dia de sobra: mostra em dias ("N dias"/"1 dia", arredondado pra CIMA — pedido do
    // usuário: "converta essas horas em dias"). < 1 dia: volta pro H:MM:SS de sempre ("quando
    // faltar um dia você coloca as horas") — só nesse trecho final é que a contagem precisa ser
    // precisa ao segundo; em dias, arredondar pra cima é suficiente e mais legível.
    public static string FormatCountdownUntil(DateTime targetUtc)
    {
        TimeSpan remaining = targetUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) return "Disponível agora";

        if (remaining.TotalDays >= 1)
        {
            int days = (int)Math.Ceiling(remaining.TotalDays);
            return days == 1 ? "1 dia" : $"{days} dias";
        }

        int totalHours = (int)remaining.TotalHours;
        return $"{totalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    public static void Set(int coins, int diamonds, int energyCurrent, int energyMax)
    {
        Coins = coins;
        Diamonds = diamonds;
        EnergyCurrent = energyCurrent;
        EnergyMax = energyMax;
        IsLoaded = true;
    }

    // Texto pronto pro HUD/popup — null só quando LastEnergyTimestampUtc ainda não foi
    // populado nesta sessão (ninguém chamou GetOrRegenAsync ainda; chamador decide esconder o
    // label nesse caso). "Energia cheia" quando já bateu o teto (nada a contar). Formato
    // H:MM:SS (2026-07-20, pedido do usuário — era HH:MM:SS/"00:00:00", sem zero à esquerda nas
    // horas agora, ex "0:00:00") — usa TotalHours (não só Hours) pra continuar correto mesmo se
    // o usuário ajustar EnergySettings.regenIntervalHours pra mais de 24h no futuro.
    // Sem prefixo "Próxima energia em" (2026-07-20, 2ª rodada, pedido do usuário) — único chamador
    // hoje é o popup de MainMenuController.ShowEnergyStatusPopup, que já mostra um título
    // ("Tempo até a próxima energia:") acima do valor; o prefixo ficava redundante.
    public static string FormatEnergyCountdown()
    {
        if (LastEnergyTimestampUtc == default) return null;
        if (EnergyCurrent >= EnergyMax) return "Energia cheia";

        DateTime nextTick = LastEnergyTimestampUtc.AddHours(RegenIntervalHours);
        TimeSpan remaining = nextTick - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        int totalHours = (int)remaining.TotalHours;
        return $"{totalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    // Verdadeiro quando o relógio LOCAL já passou do instante em que a próxima energia deveria
    // ter chegado, mas `EnergyCurrent` ainda não foi incrementado — só acontece porque o valor
    // real só avança via `EnergyService.GetOrRegenAsync` (chamado em pontos específicos: login,
    // abertura do menu, clique em Jogar), nunca sozinho enquanto o jogador só olha o timer.
    // **Bug real corrigido (2026-07-20)**: sem isto, o countdown chegava em "0:00:00" e travava
    // ali pra sempre — `remaining` é sempre clampado em zero (`FormatEnergyCountdown`), então nada
    // reativava um novo `GetOrRegenAsync`. Usado por `CountdownLabel` (ver `isDoneCheck`) pra
    // saber quando pedir um re-sync automático (ver `MainMenuCharacterPreview.BuildEnergyTimer`).
    public static bool EnergyCountdownAtZero()
    {
        if (LastEnergyTimestampUtc == default) return false;
        if (EnergyCurrent >= EnergyMax) return false;
        DateTime nextTick = LastEnergyTimestampUtc.AddHours(RegenIntervalHours);
        return DateTime.UtcNow >= nextTick;
    }
}
