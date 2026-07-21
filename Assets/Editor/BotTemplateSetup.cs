using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Popula CharacterDatabase.botTemplates com 12 PlayerProfile já existentes, escolhidos só pela
// identidade visual (prefab/ícone/splash art) — os stats/skills/armas reais dos bots são gerados
// em runtime por BotProfileGenerator, nunca lidos destes assets além do visual. Trocar a lista
// abaixo e rodar o menu de novo é o jeito de trocar quais personagens emprestam a aparência.
//
// Pedido do usuário (2026-07-15): maioria "normal" + completar com alguns "incomuns". O campo
// `PlayerProfile.rarity` (CharacterRarity) não serve pra essa curadoria hoje — os 72 profiles do
// projeto têm `rarity = Normal` (0) sem exceção (nenhum foi promovido a Uncommon/Rare/etc. ainda),
// então filtrar pelo enum literal devolveria os 72 misturados sem distinção nenhuma. Em vez disso,
// a lista abaixo já é curada nesse espírito: 9 arquétipos humanos/mundanos (guerreiros, ninjas,
// caçadores) + 3 claramente fantásticos/monstruosos (Skull Knight, Goblin, Death Knight) pra dar
// variedade sem virar maioria. Se o plano for atribuir `rarity` de verdade a esses personagens
// depois, revisitar esta lista pra ler de `CharacterDatabase.unlockedCharacters` filtrado por
// rarity em vez do array fixo.
public static class BotTemplateSetup
{
    private static readonly string[] BotTemplateNames = {
        // "Normais" (humanos/mundanos)
        "Amazon Warrior 3",
        "Samurai 1",
        "Black Ninja",
        "White Ninja",
        "Vampire Hunter 1",
        "Pirate",
        "Valkyrie 1",
        "Egyptian Sentry",
        "Blacksmith Guy",
        // "Incomuns" (fantásticos/monstruosos)
        "Skull Knight",
        "Goblin",
        "Death Knight",
    };

    private const string CharacterDatabasePath = "Assets/ScriptableObjects/Databases/CharacterDatabase.asset";

    [MenuItem("Tools/AutoArms/Setup Bot Templates")]
    public static void SetupBotTemplates()
    {
        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabasePath);
        if (db == null)
        {
            Debug.LogError($"[BotTemplateSetup] CharacterDatabase não encontrado em {CharacterDatabasePath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:PlayerProfile",
            new[] { "Assets/ScriptableObjects/PlayerProfiles" });

        var byName = new Dictionary<string, PlayerProfile>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(path);
            if (profile != null) byName[profile.name] = profile;
        }

        var templates = new List<PlayerProfile>();
        foreach (var templateName in BotTemplateNames)
        {
            if (byName.TryGetValue(templateName, out var profile))
            {
                templates.Add(profile);
            }
            else
            {
                Debug.LogError($"[BotTemplateSetup] PlayerProfile \"{templateName}\" não encontrado em Assets/ScriptableObjects/PlayerProfiles — pulado.");
            }
        }

        db.botTemplates = templates;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BotTemplateSetup] {templates.Count}/{BotTemplateNames.Length} templates de bot configurados em CharacterDatabase.");
    }
}
