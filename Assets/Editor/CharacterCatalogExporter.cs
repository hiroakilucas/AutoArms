using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Exporta {characterTypeId: rarity} de CharacterDatabase.unlockedCharacters pra um JSON que a
// Cloud Function purchaseCase le em runtime (2026-07-23, sistema de compra de personagens/case
// opening - ver ARQUITETURA.md "Modelo de roster multi-personagem"). A function roda em Node,
// sem acesso a ScriptableObjects, entao precisa da propria copia do catalogo - rodar esta
// ferramenta de novo (e reimplantar a function, `firebase deploy --only functions`) sempre que o
// roster de personagens ou alguma raridade mudar no projeto.
public static class CharacterCatalogExporter
{
    private const string DatabasePath = "Assets/ScriptableObjects/Databases/CharacterDatabase.asset";

    // Caminho relativo à raiz do repo Unity (AutoArms/) - o mesmo nivel de firebase.json/
    // firestore.rules, ver plano do sistema de compra de personagens.
    private const string OutputRelativePath = "functions/src/characterCatalog.json";

    [MenuItem("Tools/AutoArms/Export Character Catalog for Cloud Function")]
    public static void Execute()
    {
        var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
        if (database == null)
        {
            Debug.LogError($"[CharacterCatalogExporter] CharacterDatabase não encontrado em: {DatabasePath}");
            return;
        }

        var sb = new StringBuilder();
        sb.Append("{\n");
        int written = 0;
        int total = database.unlockedCharacters.Count;
        foreach (var profile in database.unlockedCharacters)
        {
            if (profile == null) continue;
            written++;
            sb.Append("  \"").Append(EscapeJson(profile.name)).Append("\": ").Append((int)profile.rarity);
            sb.Append(written < total ? ",\n" : "\n");
        }
        sb.Append("}\n");

        // Application.dataPath = ".../AutoArms/Assets" - sobe 1 nível pra chegar na raiz do repo
        // (mesmo nível de functions/), onde firebase.json/firestore.rules também vivem.
        string repoRoot = Path.GetDirectoryName(Application.dataPath);
        string outputPath = Path.Combine(repoRoot, OutputRelativePath);

        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
        {
            Debug.LogError($"[CharacterCatalogExporter] Pasta de destino não existe: {Path.GetDirectoryName(outputPath)} — rodar `firebase init functions`/conferir se functions/src existe antes.");
            return;
        }

        File.WriteAllText(outputPath, sb.ToString());
        Debug.Log($"[CharacterCatalogExporter] {written} personagens exportados para {outputPath}. Rode `npm run build` em functions/ e reimplante (`firebase deploy --only functions`) pra function passar a enxergar o catálogo atualizado.");
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
