using UnityEngine;

[CreateAssetMenu(fileName = "SelectedProfileHolder", menuName = "Game/Selected Profile Holder", order = 101)]
public class SelectedProfileHolder : ScriptableObject
{
    public PlayerProfile currentProfile;

    // ID de instância do roster do personagem atualmente selecionado (2026-07-24, sistema de
    // compra de personagens/case opening — ver ARQUITETURA.md "Modelo de roster
    // multi-personagem") — independente da identidade do objeto PlayerProfile em si, que pode
    // ser um asset pré-autorado OU uma instância runtime reconstruída via
    // PlayerProfileConverter.FromCharacterDTO (sem referência estável entre sessões). Mantido em
    // sincronia com currentProfile por SetProfile() — nunca escrever currentProfile direto sem
    // passar por aqui, senão este campo fica desatualizado.
    public string characterId;

    public void SetProfile(PlayerProfile profile)
    {
        currentProfile = profile;
        characterId = profile != null ? profile.OpponentId() : null;
    }

    public PlayerProfile GetProfile()
    {
        return currentProfile;
    }
}
