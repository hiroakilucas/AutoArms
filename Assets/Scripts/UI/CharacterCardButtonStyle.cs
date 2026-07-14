using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Restyla Btn_SelectCharacter ("Chibers") como um card estilo Brawl Stars (ícone + faixa de
// label + badge de notificação) em runtime, no mesmo espírito de UIShapeUtil.RoundedRect já
// usado por CharacterPanel — evita depender de sprites externos até a arte final existir. Cores
// vêm de UITheme (ver UI_PALETTE.md) em vez de hex hardcoded, mesma regra do resto do projeto.
public class CharacterCardButtonStyle : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private Sprite iconOverride;

    private TMP_Text _badgeText;
    private GameObject _badgeGo;

    void Awake()
    {
        if (theme == null) return;
        Build();
    }

    private void Build()
    {
        var root = (RectTransform)transform;

        var bg = GetComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(theme.secondaryButton, 16f);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var iconRt = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRt.SetParent(root, false);
        iconRt.anchorMin = new Vector2(0.08f, 0.30f);
        iconRt.anchorMax = new Vector2(0.92f, 0.92f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        if (iconOverride != null)
        {
            iconImg.sprite = iconOverride;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
        }
        else
        {
            var placeholderColor = Color.Lerp(theme.secondaryButton, Color.white, 0.35f);
            iconImg.sprite = UIShapeUtil.RoundedRect(placeholderColor, 10f);
            iconImg.type = Image.Type.Sliced;
        }

        var stripRt = new GameObject("LabelStrip", typeof(RectTransform)).GetComponent<RectTransform>();
        stripRt.SetParent(root, false);
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0.30f);
        stripRt.offsetMin = Vector2.zero;
        stripRt.offsetMax = Vector2.zero;
        var stripImg = stripRt.gameObject.AddComponent<Image>();
        stripImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 16f);
        stripImg.type = Image.Type.Sliced;

        var label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.rectTransform.SetParent(stripRt, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.fontStyle = FontStyles.Bold;
            label.color = theme.textOnDark;
            label.outlineWidth = 0.2f;
            label.outlineColor = theme.panelBackground;
            // Faixa de label é uma fração pequena da altura do card — auto-size em vez de um
            // fontSize fixo, pra continuar legível em qualquer tamanho de botão.
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 40f;
        }

        BuildNotificationBadge(root);
    }

    private void BuildNotificationBadge(RectTransform root)
    {
        var badgeRt = new GameObject("NotificationBadge", typeof(RectTransform)).GetComponent<RectTransform>();
        badgeRt.SetParent(root, false);
        badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);
        badgeRt.sizeDelta = new Vector2(44f, 44f);
        badgeRt.anchoredPosition = new Vector2(-8f, -8f);
        _badgeGo = badgeRt.gameObject;

        var badgeImg = _badgeGo.AddComponent<Image>();
        badgeImg.sprite = UIShapeUtil.RoundedRect(theme.danger, 22f);

        var textRt = new GameObject("Count", typeof(RectTransform)).GetComponent<RectTransform>();
        textRt.SetParent(badgeRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        _badgeText = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        _badgeText.alignment = TextAlignmentOptions.Center;
        _badgeText.fontSize = 24;
        _badgeText.fontStyle = FontStyles.Bold;
        _badgeText.color = theme.textOnDark;
        _badgeText.text = "0";

        _badgeGo.SetActive(false);
    }

    // Chamar quando houver notificação real (ex: contador de novos personagens disponíveis).
    public void SetNotificationCount(int count)
    {
        if (_badgeGo == null) return;
        if (count <= 0)
        {
            _badgeGo.SetActive(false);
            return;
        }
        _badgeGo.SetActive(true);
        if (_badgeText != null) _badgeText.text = count.ToString();
    }
}
