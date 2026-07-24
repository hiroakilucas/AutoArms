using System.Collections.Generic;

// Canal estático cross-scene (mesmo padrão de PlayerEconomyState/PlayerProgressionState/etc,
// 2026-07-23, sistema de compra de personagens/case opening) — cache dos documentos de
// casePackages/ + contador de compras por jogador (casePurchases/), lidos uma vez por
// ShopController.LoadPersistedShopStateAsync e reaproveitados por BuildItemData sem round-trip
// extra a cada troca de aba/RebuildGrid. Fonte de verdade continua sendo o Firestore (ver
// CasePackageService) — este canal só evita reconsultar toda hora.
public static class CasePackageState
{
    public static readonly Dictionary<string, CasePackageService.CasePackageInfo> Packages =
        new Dictionary<string, CasePackageService.CasePackageInfo>();

    public static readonly Dictionary<string, int> PurchasedCounts = new Dictionary<string, int>();
}
