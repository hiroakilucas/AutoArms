using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterSelectUI : MonoBehaviour
{
    public Transform gridContainer;
    public GameObject cardPrefab;
    public PlayerProfile[] availableProfiles;

    private PlayerProfile selected;

    void Start()
    {
        foreach (var profile in availableProfiles)
        {
            GameObject card = Instantiate(cardPrefab, gridContainer);
            card.GetComponentInChildren<TMP_Text>().text = profile.profileName;
            card.GetComponentInChildren<Image>().sprite = profile.previewIcon;

            Button btn = card.GetComponent<Button>();
            btn.onClick.AddListener(() => OnSelect(profile));
        }
    }

    public void OnSelect(PlayerProfile profile)
    {
        selected = profile;
        Debug.Log($"Selecionado: {profile.profileName}");
    }

    public void OnConfirm()
    {
        if (selected == null)
        {
            Debug.LogWarning("Nenhum personagem selecionado.");
            return;
        }

        SelectedProfileHolder.Instance.selectedProfile = selected;
        SceneManager.LoadScene("01_MainMenu");
    }

    public void OnBack()
    {
        SceneManager.LoadScene("01_MainMenu");
    }
}
