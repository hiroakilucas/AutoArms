using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterCardUI : MonoBehaviour
{
    public Image icon;              // ← Atribuído no prefab
    public TMP_Text nameText;       // ← Atribuído no prefab

    private PlayerProfile profile;
    private CharacterSelectController controller;

    public void Setup(PlayerProfile profile, CharacterSelectController controller)
    {
        this.profile = profile;
        this.controller = controller;

        // Aqui ocorre o erro se 'icon' ou 'nameText' estiver null
        icon.sprite = profile.previewIcon;
        //nameText.text = profile.profileName;
    }

    public void OnClick()
    {
        controller?.OnCharacterSelected(profile);
    }
}
