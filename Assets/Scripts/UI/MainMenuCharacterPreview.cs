using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuCharacterPreview : MonoBehaviour
{
    public Transform spawnPoint;
    private GameObject currentCharacter;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;

        if (profile == null)
        {
            Debug.LogWarning("Nenhum personagem selecionado para o Main Menu.");
            return;
        }

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale;
        currentCharacter.transform.position = new Vector3(-2f, -2, 0);
        Camera.main.orthographicSize = 5;

        DestroyImmediate(currentCharacter.GetComponent<PlayerCombat>());
        DestroyImmediate(currentCharacter.GetComponent<WeaponHandler>());
        DestroyImmediate(currentCharacter.GetComponent<MovementController>());
        DestroyImmediate(currentCharacter.GetComponent<AnimationController>());

        var anim = currentCharacter.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Idle", true);

        BuildSummaryHUD(profile);
    }

    private void BuildSummaryHUD(PlayerProfile p)
    {
        var canvasGo = new GameObject("SummaryHUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Container centrado na parte inferior
        var container = new GameObject("Container");
        container.transform.SetParent(canvasGo.transform, false);
        // RT first, then Image — safe order
        var crt = container.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.30f, 0.03f);
        crt.anchorMax = new Vector2(0.70f, 0.22f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        container.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Nome + Level
        var nameGo = new GameObject("NameLevel");
        nameGo.transform.SetParent(container.transform, false);
        // RT first so AddComponent<TMP> doesn't stomp it
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.68f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = $"{p.profileName}  <size=13><color=#C8A044>Level {p.level}</color></size>";
        nameTxt.fontSize = 16;
        nameTxt.color = new Color(0.90f, 0.78f, 0.35f, 1f);
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;

        // XP bar background
        var xpBgGo = new GameObject("XpBg");
        xpBgGo.transform.SetParent(container.transform, false);
        var xbrt = xpBgGo.AddComponent<RectTransform>();
        xbrt.anchorMin = new Vector2(0.04f, 0.52f); xbrt.anchorMax = new Vector2(0.96f, 0.66f);
        xbrt.offsetMin = xbrt.offsetMax = Vector2.zero;
        xpBgGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        // XP fill
        int req = XpSystem.XpRequired(p.level);
        float pct = req > 0 ? Mathf.Clamp01((float)p.xpCurrent / req) : 0f;
        var xpFillGo = new GameObject("XpFill");
        xpFillGo.transform.SetParent(xpBgGo.transform, false);
        var xfrt = xpFillGo.AddComponent<RectTransform>();
        xfrt.anchorMin = Vector2.zero;
        xfrt.anchorMax = new Vector2(pct, 1f);
        xfrt.offsetMin = xfrt.offsetMax = Vector2.zero;
        xpFillGo.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.90f, 0.90f);

        // XP label
        var xpLblGo = new GameObject("XpLabel");
        xpLblGo.transform.SetParent(container.transform, false);
        var xlrt = xpLblGo.AddComponent<RectTransform>();
        xlrt.anchorMin = new Vector2(0f, 0.38f); xlrt.anchorMax = new Vector2(1f, 0.52f);
        xlrt.offsetMin = xlrt.offsetMax = Vector2.zero;
        var xpTxt = xpLblGo.AddComponent<TextMeshProUGUI>();
        xpTxt.text = $"XP {p.xpCurrent}/{req}";
        xpTxt.fontSize = 9; xpTxt.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        xpTxt.alignment = TextAlignmentOptions.Center;

        // Skill icons (first 3)
        var iconRow = new GameObject("SkillIcons");
        iconRow.transform.SetParent(container.transform, false);
        var irt = iconRow.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.20f, 0.02f); irt.anchorMax = new Vector2(0.80f, 0.36f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var hlg = iconRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        int shown = 0;
        if (p.skills != null)
        {
            foreach (var skill in p.skills)
            {
                if (skill == null || shown >= 3) continue;
                shown++;
                var iconGo = new GameObject($"Skill{shown}");
                iconGo.transform.SetParent(iconRow.transform, false);
                // RT first, then Image
                var icrt = iconGo.AddComponent<RectTransform>();
                icrt.sizeDelta = new Vector2(36f, 36f);
                iconGo.AddComponent<Image>().color = new Color(0.78f, 0.63f, 0.27f, 0.30f);

                if (skill.icon != null)
                {
                    var spGo = new GameObject("Spr");
                    spGo.transform.SetParent(iconGo.transform, false);
                    var sprt = spGo.AddComponent<RectTransform>();
                    sprt.anchorMin = new Vector2(0.06f, 0.06f);
                    sprt.anchorMax = new Vector2(0.94f, 0.94f);
                    sprt.offsetMin = sprt.offsetMax = Vector2.zero;
                    var spImg = spGo.AddComponent<Image>();
                    spImg.sprite = skill.icon; spImg.preserveAspect = true;
                }
            }
        }
    }
}
