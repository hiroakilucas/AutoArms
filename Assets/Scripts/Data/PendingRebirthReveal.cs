using System.Collections.Generic;

// Canal estático cross-scene mínimo (mesmo padrão de PendingCharacterSelection/
// ReplayPlaybackState) — 2026-07-26, feature "Renascimento". CharacterPanel.ExecuteRebirthAsync
// grava aqui os itens já concedidos (server-side, já persistidos/aplicados ao profile) antes de
// navegar pra 02_SelectCharacter — CharacterSelectController.OnCharacterSelected lê e LIMPA este
// campo assim que reconhece o personagem certo, disparando a revelação em sequência (mesma
// CharacterUnlockRevealPanel já usada pelo case opening) sobre o fundo com a splash art do
// personagem, em vez de revelar inline em cima de qualquer tela onde o botão RENASCIMENTO foi
// clicado (01_MainMenu/03_Arsenal).
//
// Diferente de PendingCharacterSelection: aqui os itens JÁ FORAM aplicados a
// profile.skills/weapons/pets (ver ExecuteRebirthAsync) — a revelação em 02_SelectCharacter é só
// cerimônia visual + oportunidade de reroll individual, não um "rascunho ainda não aceito" (por
// isso não precisa de persistência resiliente a fechar o app no meio, ao contrário dos campos
// pendingUnlock*/pendingLevelUpBoxes de PlayerProfile).
public static class PendingRebirthReveal
{
    public class GrantedItemRef
    {
        public string Kind; // "skill" | "weapon" | "pet"
        public string Name;
        public int Tier;
    }

    public static string PendingCharacterId;
    public static List<GrantedItemRef> GrantedItems;
}
