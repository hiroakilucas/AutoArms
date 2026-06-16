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
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

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
    }

    public void OnConfirm()
    {
        if (selected == null) return;

        selectedProfileHolder.currentProfile = selected;
        SceneManager.LoadScene("01_MainMenu");
    }

    public void OnBack()
    {
        SceneManager.LoadScene("01_MainMenu");
    }
}
