using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Exporta as familias de skill/arma/pet (nome + odds + quais tiers realmente existem) pra um JSON
// que a Cloud Function rerollUnlock le em runtime (2026-07-25, refresh dos unlocks progressivos
// do case opening - ver ARQUITETURA.md/MONETIZACAO.md). Mesmo espirito de
// CharacterCatalogExporter.cs - a function roda em Node, sem acesso a ScriptableObjects, entao
// precisa da propria copia dos databases. Rodar esta ferramenta de novo (e reimplantar a
// function, `firebase deploy --only functions`) sempre que odds mudarem (Tools > AutoArms >
// Apply My Brute Odds) ou um tier novo for gerado (Generate Weapon/Pet/Skill Tiers).
public static class UnlockCatalogExporter
{
    private const string SkillDatabasePath = "Assets/Resources/SkillDatabase.asset";
    private const string WeaponDatabasePath = "Assets/Resources/WeaponDatabase.asset";
    private const string PetDatabasePath = "Assets/Resources/PetDatabase.asset";

    // Caminho relativo à raiz do repo Unity (AutoArms/) - mesmo nível de firebase.json.
    private const string OutputRelativePath = "functions/src/unlockCatalog.json";

    private struct FamilyEntry
    {
        public float odds;
        public HashSet<int> tiers;
    }

    [MenuItem("Tools/AutoArms/Export Unlock Catalog for Cloud Function")]
    public static void Execute()
    {
        var skillDb = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabasePath);
        var weaponDb = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(WeaponDatabasePath);
        var petDb = AssetDatabase.LoadAssetAtPath<PetDatabase>(PetDatabasePath);

        var skillFamilies = new Dictionary<string, FamilyEntry>();
        if (skillDb != null)
        {
            foreach (var s in skillDb.skills)
            {
                if (s == null || string.IsNullOrEmpty(s.skillName)) continue;
                AddTier(skillFamilies, s.skillName, s.odds, s.tier);
            }
        }
        else
        {
            Debug.LogError($"[UnlockCatalogExporter] SkillDatabase não encontrado em: {SkillDatabasePath}");
        }

        // WeaponDatabase.weapons só lista os T1 (raiz de cada família) - sobe nextTier pra achar
        // T2/T3, mesmo padrão de ResolveTierFamily/CharacterUnlockEngine.
        var weaponFamilies = new Dictionary<string, FamilyEntry>();
        if (weaponDb != null)
        {
            foreach (var root in weaponDb.weapons)
            {
                var w = root;
                while (w != null)
                {
                    string family = WeaponNameUtil.StripWeaponTierSuffix(w.weaponName);
                    AddTier(weaponFamilies, family, w.dropOdds, w.tier);
                    w = w.nextTier;
                }
            }
        }
        else
        {
            Debug.LogError($"[UnlockCatalogExporter] WeaponDatabase não encontrado em: {WeaponDatabasePath}");
        }

        // PetDatabase.pets só lista os T1 - mesma lógica de WeaponDatabase acima.
        var petFamilies = new Dictionary<string, FamilyEntry>();
        if (petDb != null)
        {
            foreach (var root in petDb.pets)
            {
                var p = root;
                while (p != null)
                {
                    AddTier(petFamilies, p.petType.ToString(), p.odds, p.tier);
                    p = p.nextTier;
                }
            }
        }
        else
        {
            Debug.LogError($"[UnlockCatalogExporter] PetDatabase não encontrado em: {PetDatabasePath}");
        }

        var sb = new StringBuilder();
        sb.Append("{\n");
        AppendFamilyArray(sb, "skills", skillFamilies);
        sb.Append(",\n");
        AppendFamilyArray(sb, "weapons", weaponFamilies);
        sb.Append(",\n");
        AppendFamilyArray(sb, "pets", petFamilies);
        sb.Append("\n}\n");

        // Application.dataPath = ".../AutoArms/Assets" - sobe 1 nível pra chegar na raiz do repo
        // (mesmo nível de functions/).
        string repoRoot = Path.GetDirectoryName(Application.dataPath);
        string outputPath = Path.Combine(repoRoot, OutputRelativePath);

        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
        {
            Debug.LogError($"[UnlockCatalogExporter] Pasta de destino não existe: {Path.GetDirectoryName(outputPath)}");
            return;
        }

        File.WriteAllText(outputPath, sb.ToString());
        Debug.Log($"[UnlockCatalogExporter] {skillFamilies.Count} skills, {weaponFamilies.Count} armas, {petFamilies.Count} pets exportados para {outputPath}. Rode `npm run build` em functions/ e reimplante (`firebase deploy --only functions`) pra rerollUnlock passar a enxergar o catálogo atualizado.");
    }

    private static void AddTier(Dictionary<string, FamilyEntry> families, string name, float odds, int tier)
    {
        if (!families.TryGetValue(name, out var entry))
            entry = new FamilyEntry { odds = odds, tiers = new HashSet<int>() };
        entry.tiers.Add(tier);
        families[name] = entry;
    }

    private static void AppendFamilyArray(StringBuilder sb, string key, Dictionary<string, FamilyEntry> families)
    {
        sb.Append("  \"").Append(key).Append("\": [\n");
        var names = new List<string>(families.Keys);
        for (int i = 0; i < names.Count; i++)
        {
            var entry = families[names[i]];
            var tiers = new List<int>(entry.tiers);
            tiers.Sort();
            sb.Append("    { \"name\": \"").Append(EscapeJson(names[i])).Append("\", \"odds\": ")
              .Append(entry.odds.ToString(CultureInfo.InvariantCulture)).Append(", \"tiers\": [")
              .Append(string.Join(", ", tiers)).Append("] }");
            sb.Append(i < names.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]");
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
