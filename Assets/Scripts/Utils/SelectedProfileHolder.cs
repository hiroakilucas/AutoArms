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

    // Bug real corrigido (2026-07-25, achado via log de diagnóstico com instanceID — reportado
    // pelo usuário: SelectedProfileHolder voltava pro default do asset ("Medieval Warrior",
    // characterId vazio) só de ir em 06_Loja e voltar, mesmo SEM nenhum código chamando
    // SetProfile no meio) — `SceneManager.LoadScene` em modo Single roda `Resources.
    // UnloadUnusedAssets` implicitamente a cada troca de cena; `06_Loja`/`ShopController` é a
    // única cena do fluxo sem NENHUMA referência serializada a este asset (vive em
    // Assets/Resources/, sem GameObject nenhum de 06_Loja apontando pra ele) — por um instante,
    // entre destruir os objetos de 01_MainMenu e construir os de 06_Loja, nada na hierarquia
    // ativa o referenciava, tornando-o elegível pra descarregar; ao voltar, a Unity recarregava
    // do zero a partir do estado serializado em disco (o default do Inspector), perdendo
    // currentProfile/characterId setados em runtime. `HideFlags.DontUnloadUnusedAsset` protege
    // contra isso incondicionalmente, independente de qual cena referencia o asset no momento —
    // fix definitivo, não depende de toda cena futura lembrar de wirear este campo.
    private void OnEnable()
    {
        hideFlags |= HideFlags.DontUnloadUnusedAsset;
    }

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
