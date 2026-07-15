// Extraído de CharacterPanel.StripTierSuffix (2026-07-14) pra ser reaproveitado também pela
// camada de save (LocalSaveService/PlayerProfileConverter), que precisa do mesmo nome "de
// família" (sem o sufixo de tier embutido em WeaponData.weaponName) pra procurar a arma de volta
// em WeaponDatabase na hora de restaurar um save.
public static class WeaponNameUtil
{
    private static readonly string[] TierSuffixes = { " T1", " T2", " T3" };

    // Remove o sufixo " T1"/" T2"/" T3" que vem embutido no próprio WeaponData.weaponName do
    // asset (ex: "Knife T1" -> "Knife").
    public static string StripWeaponTierSuffix(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return weaponName;
        foreach (var suffix in TierSuffixes)
            if (weaponName.EndsWith(suffix)) return weaponName.Substring(0, weaponName.Length - suffix.Length);
        return weaponName;
    }
}
