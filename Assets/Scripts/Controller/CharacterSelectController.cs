using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterSelectController : MonoBehaviour
{
    [Header("Referências principais")]
    public GameObject previewPanel;
    public GameObject gridPanel;
    public GameObject detailPanel;

    [Header("Componentes do preview")]
    public Image previewPortrait;
    public TMP_Text txtLevel;
    public TMP_Text txtWinRate;
    public TMP_Text txtXp;
    public Image xpBar;
    public TMP_Text txtJogos;

    [Header("Prefabs")]
    public GameObject characterCardPrefab;
    public Transform gridContent;

    [Header("Data Sources (ScriptableObjects)")]
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private CharacterDatabase characterDatabase;

    private PlayerProfile selectedProfile;
    
    public Transform spawnPoint;
    private GameObject currentPreviewCharacter;

    void Start()
    {
        if (selectedProfileHolder.currentProfile != null)
        {
            LoadPreviewData(selectedProfileHolder.currentProfile);
        }
        previewPortrait.gameObject.SetActive(false);
        PopulateCharacterGrid();
        
    }

    public void LoadPreviewData(PlayerProfile profile)
    {
        selectedProfile = profile;

        // Atualiza ícones e textos
        previewPortrait.sprite = profile.previewIcon;
        txtLevel.text = "Lv. " + profile.level;
        txtWinRate.text = $"{profile.winRate}%";
        txtXp.text = $"{profile.xpCurrent}/{profile.xpRequired}";
        xpBar.fillAmount = (float)profile.xpCurrent / profile.xpRequired;
        txtJogos.text = $"{profile.battlesRemaining}/6";

        // Remove personagem anterior
        if (currentPreviewCharacter != null)
            Destroy(currentPreviewCharacter);

        // Instancia personagem 3D/2D
        currentPreviewCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentPreviewCharacter.transform.SetParent(spawnPoint); // Agrupa no painel
        currentPreviewCharacter.transform.localScale = profile.scale;
        currentPreviewCharacter.transform.localPosition = Vector3.zero;
        currentPreviewCharacter.transform.localRotation = Quaternion.identity;

        // Remove lógica de combate (menu)
        DestroyImmediate(currentPreviewCharacter.GetComponent<PlayerCombat>());
        DestroyImmediate(currentPreviewCharacter.GetComponent<WeaponHandler>());
        DestroyImmediate(currentPreviewCharacter.GetComponent<MovementController>());
        DestroyImmediate(currentPreviewCharacter.GetComponent<AnimationController>());

        var anim = currentPreviewCharacter.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Idle", true);
    }

    public void PopulateCharacterGrid()
    {
        foreach (Transform child in gridContent)
            Destroy(child.gameObject);

        foreach (PlayerProfile profile in characterDatabase.unlockedCharacters)
        {
            var card = Instantiate(characterCardPrefab, gridContent);
            card.GetComponent<CharacterCardUI>().Setup(profile, this);
        }
    }

    public void OnCharacterSelected(PlayerProfile profile)
    {
        selectedProfile = profile;
        ShowDetails(profile);
    }

    void ShowDetails(PlayerProfile profile)
    {
        detailPanel.SetActive(true);
        gridPanel.SetActive(false);
        LoadPreviewData(profile);
    }

    public void OnClickBackToCharacters()
    {
        detailPanel.SetActive(false);
        gridPanel.SetActive(true);
    }

    public void OnClickSelect()
    {
        selectedProfileHolder.currentProfile = selectedProfile;
        SceneManager.LoadScene("01_MainMenu");
    }
}
