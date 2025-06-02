using UnityEngine;

public class MainMenuCharacterPreview : MonoBehaviour
{
    public Transform spawnPoint;
    private GameObject currentCharacter;

    void Start()
    {
        var profile = SelectedProfileHolder.Instance?.selectedProfile;
        if (profile == null)
        {
            Debug.Log("Nenhum personagem selecionado.");
            return;
        }

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale;
        currentCharacter.transform.position = new Vector3(0, -2, 0); // Ajuste conforme sua câmera
        Camera.main.orthographicSize = 5; // Garantir visibilidade
        // Remove componentes que não queremos rodando no menu
        DestroyImmediate(currentCharacter.GetComponent<PlayerCombat>());
        DestroyImmediate(currentCharacter.GetComponent<WeaponHandler>());
        DestroyImmediate(currentCharacter.GetComponent<MovementController>());
        DestroyImmediate(currentCharacter.GetComponent<AnimationController>());

        // Força animação idle, se houver Animator
        var anim = currentCharacter.GetComponent<Animator>();
        if (anim != null)
            anim.SetBool("Idle", true);
    }
}
