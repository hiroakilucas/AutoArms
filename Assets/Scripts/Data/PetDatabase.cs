using UnityEngine;
using System.Collections.Generic;

// Lista central dos 3 T1 de pet (mesmo espírito de WeaponDatabase/SkillDatabase) — usada pela
// camada de save (LocalSaveService/PlayerProfileConverter) pra resolver de volta o PetData real
// a partir do par tipo+tier gravado no save (ScriptableObjects não podem ser serializados em
// JSON/Firestore diretamente). Populado automaticamente por
// Tools > AutoArms > Generate Pet Tiers (T1, T2 & T3) — não precisa curadoria manual, só existem
// 3 pets no total. Precisa viver em Assets/Resources/ (mesmo motivo de WeaponDatabase/
// SkillDatabase) pra PlayerProfileConverter conseguir resolvê-la via Resources.Load em runtime,
// sem depender de nenhuma cena específica ter uma referência [SerializeField] pra ela.
[CreateAssetMenu(fileName = "PetDatabase", menuName = "Game/Pet Database", order = 104)]
public class PetDatabase : ScriptableObject
{
    public List<PetData> pets = new List<PetData>();

    // Acha o PetData exato (T1/T2/T3) a partir do PetType e do tier desejado — `pets` só lista
    // os T1 (raiz de cada família); sobe até o tier pedido andando por PetData.nextTier, mesmo
    // padrão de WeaponDatabase.FindByFamilyNameAndTier.
    public PetData FindByTypeAndTier(PetType type, int tier)
    {
        if (tier < 1) return null;
        foreach (var t1 in pets)
        {
            if (t1 == null || t1.petType != type) continue;

            var current = t1;
            for (int i = 1; i < tier && current != null; i++)
                current = current.nextTier;
            return current;
        }
        return null;
    }
}
