using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class CombatResultPanel : MonoBehaviour
{
    public void Show(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                     PlayerProfile profile, bool didLevelUp,
                     SkillDatabase skillDatabase = null, WeaponData[] allWeapons = null)
    {
        StartCoroutine(ShowRoutine(player1Won, xpGained, xpBefore, levelBefore,
                                   profile, didLevelUp, skillDatabase, allWeapons));
    }

    private IEnumerator ShowRoutine(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                                    PlayerProfile profile, bool didLevelUp,
                                    SkillDatabase skillDatabase, WeaponData[] allWeapons)
    {
        yield return new WaitForSeconds(0.8f);

        EnsureEventSystem();

        Canvas canvas = FindScreenCanvas();
        if (canvas == null) yield break;

        MakeOverlay(canvas.transform);

        var panel = MakePanel(canvas.transform);

        // Title
        string titleText  = player1Won ? "VITÓRIA!" : "DERROTA!";
        Color  titleColor = player1Won ? new Color(1f, 0.84f, 0f) : new Color(0.9f, 0.15f, 0.15f);
        MakeLabel(panel, titleText, 52, titleColor, new Vector2(0, 155f), new Vector2(420f, 65f), bold: true);

        // XP gained
        MakeLabel(panel, $"+{xpGained} XP", 36, new Color(0.45f, 1f, 0.45f),
            new Vector2(0, 90f), new Vector2(280f, 46f));

        // XP progress bar
        int xpRequiredBefore = XpSystem.XpRequired(levelBefore);
        int xpRequiredAfter  = XpSystem.XpRequired(profile.level);
        var barFill = MakeXpBar(panel, new Vector2(0f, 45f), new Vector2(380f, 22f));

        // XP label
        string initialXpText = didLevelUp
            ? $"{xpRequiredBefore} / {xpRequiredBefore} XP"
            : $"{profile.xpCurrent} / {xpRequiredBefore} XP";
        var xpLabel = MakeLabel(panel, initialXpText, 21, Color.white,
            new Vector2(0f, 12f), new Vector2(320f, 28f));

        MakeLabel(panel, $"Level {profile.level}", 26, new Color(0.82f, 0.82f, 0.82f),
            new Vector2(0f, -26f), new Vector2(280f, 34f));

        MakeLabel(panel, $"Batalhas restantes: {profile.battlesRemaining} / 6", 20,
            new Color(0.65f, 0.65f, 0.65f),
            new Vector2(0f, -66f), new Vector2(380f, 30f));

        // Level-up text (hidden until needed)
        var levelUpLabel = MakeLabel(panel, $"LEVEL UP!  →  Level {profile.level}", 30,
            new Color(1f, 0.84f, 0f),
            new Vector2(0f, -110f), new Vector2(420f, 40f), bold: true);
        levelUpLabel.gameObject.SetActive(false);

        // Continue button — disabled until choice is made when leveling up
        bool choiceDone = false;
        var continueBtn = MakeButton(panel, "Continuar", new Vector2(0f, -172f),
            () => SceneManager.LoadScene("01_MainMenu"));
        continueBtn.interactable = !didLevelUp;

        // Animate XP bar
        float startFill  = xpRequiredBefore > 0 ? (float)xpBefore / xpRequiredBefore : 0f;
        float targetFill = didLevelUp ? 1f
            : (xpRequiredBefore > 0 ? (float)profile.xpCurrent / xpRequiredBefore : 0f);

        yield return AnimateBar(barFill, startFill, targetFill, 0.75f);

        if (didLevelUp)
        {
            yield return new WaitForSeconds(0.15f);
            levelUpLabel.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                float s = 1f + Mathf.Sin(elapsed / 0.5f * Mathf.PI) * 0.28f;
                levelUpLabel.transform.localScale = Vector3.one * s;
                elapsed += Time.deltaTime;
                yield return null;
            }
            levelUpLabel.transform.localScale = Vector3.one;

            barFill.fillAmount = xpRequiredAfter > 0 ? (float)profile.xpCurrent / xpRequiredAfter : 0f;
            xpLabel.text = $"{profile.xpCurrent} / {xpRequiredAfter} XP";

            ShowLevelUpChoice(canvas.transform, profile, skillDatabase, allWeapons, () => {
                choiceDone = true;
                continueBtn.interactable = true;
            });

            yield return new WaitUntil(() => choiceDone);
        }
    }

    // ── Level-Up Choice Panel ────────────────────────────────────────────────

    private struct LevelUpOption
    {
        public enum Kind { Attribute, Skill, Weapon, Pet }
        public Kind kind;
        public int attrIndex;
        public SkillData skill;
        public WeaponData weapon;
        public PetType petType;

        public string Name() => kind switch {
            Kind.Attribute => new[] {
                "+12 HP", "+2 STR", "+2 AGI", "+2 SPD",
                "+1 STR / +1 AGI", "+1 AGI / +1 SPD", "+1 STR / +1 SPD",
                "+6 HP / +1 STR", "+6 HP / +1 AGI", "+6 HP / +1 SPD"
            }[attrIndex],
            Kind.Skill     => skill?.skillName ?? "?",
            Kind.Weapon    => weapon?.weaponName ?? "?",
            Kind.Pet       => PetState.DisplayName(petType),
            _              => "?"
        };

        public string Desc() => kind switch {
            Kind.Attribute => new[] {
                "Vida máxima +12",
                "Força +2",
                "Agilidade +2",
                "Velocidade +2",
                "Força +1, Agilidade +1",
                "Agilidade +1, Velocidade +1",
                "Força +1, Velocidade +1",
                "Vida máxima +6, Força +1",
                "Vida máxima +6, Agilidade +1",
                "Vida máxima +6, Velocidade +1"
            }[attrIndex],
            Kind.Skill  => skill?.description ?? "",
            Kind.Weapon => weapon != null ? $"{string.Join(", ", weapon.types)} • {weapon.damage} dano" : "",
            // -HP do dono (HpCost) entra logo na escolha — ApplyBonus já garante maxHealth >= 1.
            Kind.Pet    => $"Luta junto. -{PetState.HpCost(petType)} HP máximo do dono",
            _           => ""
        };

        public Color AttrColor() => kind == Kind.Attribute ? attrIndex switch {
            0 => new Color(0.8f, 0.2f, 0.2f),
            1 => new Color(1f,   0.6f, 0.1f),
            2 => new Color(0.2f, 0.7f, 0.2f),
            3 => new Color(0.3f, 0.5f, 1f),
            4 => new Color(0.9f, 0.7f, 0.1f),
            5 => new Color(0.2f, 0.8f, 0.6f),
            6 => new Color(0.7f, 0.4f, 0.9f),
            7 => new Color(0.9f, 0.4f, 0.2f),  // HP+STR — laranja-avermelhado
            8 => new Color(0.4f, 0.7f, 0.3f),  // HP+AGI — verde-médio
            9 => new Color(0.4f, 0.6f, 0.9f),  // HP+SPD — azul-médio
            _ => Color.white
        } : Color.white;
    }

    // Pool de pets sempre disponível (sem restrição de duplicatas — o mesmo tipo pode sair de
    // novo mesmo que o jogador já tenha um igual), por isso wPet não degrada a 0 como
    // wSkill/wWeapon quando o pool correspondente está vazio.
    private static readonly PetType[] PetPool = { PetType.Mouse, PetType.Monkey, PetType.Boar };

    private static LevelUpOption DrawOption(List<SkillData> skills, List<WeaponData> weapons)
    {
        float wAttr   = 0.60f;
        float wSkill  = skills.Count  > 0 ? 0.30f : 0f;
        float wWeapon = weapons.Count > 0 ? 0.10f : 0f;
        float wPet    = 0.10f;
        float total   = wAttr + wSkill + wWeapon + wPet;
        float r       = Random.value * total;

        if (r < wAttr)
            return new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = Random.Range(0, 10) };
        if (r < wAttr + wSkill)
            return new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skills[Random.Range(0, skills.Count)] };
        if (r < wAttr + wSkill + wWeapon)
            return new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = weapons[Random.Range(0, weapons.Count)] };
        return new LevelUpOption { kind = LevelUpOption.Kind.Pet, petType = PetPool[Random.Range(0, PetPool.Length)] };
    }

    private static bool SameOption(LevelUpOption a, LevelUpOption b)
    {
        if (a.kind != b.kind) return false;
        return a.kind switch {
            LevelUpOption.Kind.Attribute => a.attrIndex == b.attrIndex,
            LevelUpOption.Kind.Skill     => a.skill     == b.skill,
            LevelUpOption.Kind.Weapon    => a.weapon    == b.weapon,
            LevelUpOption.Kind.Pet       => a.petType   == b.petType,
            _                            => false
        };
    }

    private static void ApplyBonus(LevelUpOption opt, PlayerProfile profile)
    {
        switch (opt.kind)
        {
            case LevelUpOption.Kind.Attribute:
                switch (opt.attrIndex)
                {
                    case 0: profile.maxHealth += 12; break;
                    case 1: profile.str       += 2; break;
                    case 2: profile.agility   += 2; break;
                    case 3: profile.speed     += 2; break;
                    case 4: profile.str += 1; profile.agility += 1; break;
                    case 5: profile.agility += 1; profile.speed += 1; break;
                    case 6: profile.str += 1; profile.speed += 1; break;
                    case 7: profile.maxHealth += 6; profile.str     += 1; break;
                    case 8: profile.maxHealth += 6; profile.agility += 1; break;
                    case 9: profile.maxHealth += 6; profile.speed   += 1; break;
                }
                break;
            case LevelUpOption.Kind.Skill:
                if (opt.skill != null && !profile.skills.Contains(opt.skill))
                {
                    profile.skills.Add(opt.skill);
                    // Vitality / Herculean Strength / Feline Agility / Lightning Bolt: flat
                    // permanente (HP/STR/AGI/SPD) aplicado uma única vez na escolha (igual a um
                    // pick de Atributo) — o +50% restante é percentual em runtime sobre esse
                    // valor já somado (ApplySkillStats/GetEffectiveStats).
                    if (opt.skill.skillName == "Vitality")
                        profile.maxHealth += 18;
                    else if (opt.skill.skillName == "Herculean Strength")
                        profile.str += 3;
                    else if (opt.skill.skillName == "Feline Agility")
                        profile.agility += 3;
                    else if (opt.skill.skillName == "Lightning Bolt")
                        profile.speed += 3;
                    else if (opt.skill.skillName == "Reconnaissance")
                        profile.speed += 5;
                }
                break;
            case LevelUpOption.Kind.Weapon:
                if (opt.weapon != null && profile.weaponLoadout != null)
                {
                    var list = new List<WeaponData>(profile.weaponLoadout.weapons ?? new WeaponData[0]);
                    // Upgrade de tier: remove o tier anterior do loadout antes de adicionar o novo
                    if (opt.weapon.previousTier != null)
                        list.RemoveAll(lw => lw != null && lw.weaponName == opt.weapon.previousTier.weaponName);
                    list.Add(opt.weapon);
                    profile.weaponLoadout.weapons = list.ToArray();
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(profile.weaponLoadout);
#endif
                }
                break;
            case LevelUpOption.Kind.Pet:
                // Sem restrição de duplicatas — o mesmo tipo pode aparecer de novo (ex: 2º Rato).
                profile.pets.Add(opt.petType);
                profile.maxHealth = Mathf.Max(1, profile.maxHealth - PetState.HpCost(opt.petType));
                break;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
    }

    // TESTE: mostra todas as skills/armas/atributos disponíveis em vez de sortear 2 opções
    // ponderadas. Reverter para o sorteio original (DrawOption/SameOption abaixo) trocando
    // esta flag para false quando o teste terminar.
    private const bool ShowAllOptionsForTesting = true;

    private static void ShowLevelUpChoice(Transform canvasRoot, PlayerProfile profile,
        SkillDatabase skillDb, WeaponData[] allWeaponsPool, System.Action onChosen)
    {
        // Build available option pools
        if (skillDb == null || skillDb.skills == null || skillDb.skills.Count == 0)
            Debug.LogError("[LevelUp] ERRO: SkillDatabase não encontrado ou vazio");

        var availableSkills = new List<SkillData>();
        if (skillDb != null && skillDb.skills != null)
            foreach (var s in skillDb.skills)
                // s.icon != null: testando skill por skill — só entram na lista de escolha as
                // que já têm um ícone de volta em Assets/Data/UI/Skills/ (removidos todos,
                // re-adicionados um a um conforme testados; immortality é a primeira).
                if (s != null && s.icon != null && !profile.skills.Exists(ps => ps != null && ps.skillName == s.skillName))
                    availableSkills.Add(s);

        var availableWeapons = new List<WeaponData>();
        var loadoutWeapons = profile.weaponLoadout?.weapons;
        if (allWeaponsPool != null)
            foreach (var w in allWeaponsPool)
            {
                if (w == null) continue;
                if (IsInLoadout(w, loadoutWeapons)) continue;

                if (w.tier <= 1)
                {
                    // T1: só aparece se não há upgrade desta arma (T2 ou T3) no loadout
                    if (!HasUpgradeInLoadout(w, loadoutWeapons, allWeaponsPool))
                        availableWeapons.Add(w);
                }
                else
                {
                    // T2/T3: só aparece se o tier anterior está no loadout
                    if (w.previousTier != null && IsInLoadout(w.previousTier, loadoutWeapons))
                        availableWeapons.Add(w);
                }
            }

        if (ShowAllOptionsForTesting)
        {
            ShowAllOptionsChoice(canvasRoot, profile, availableSkills, availableWeapons, onChosen);
            return;
        }

        // Draw 2 unique options
        var opt1 = DrawOption(availableSkills, availableWeapons);
        LevelUpOption opt2;
        int tries = 0;
        do { opt2 = DrawOption(availableSkills, availableWeapons); tries++; }
        while (tries < 50 && SameOption(opt1, opt2));

        // Root container covers the whole canvas (renders above result panel as last sibling)
        var root = new GameObject("LevelUpChoiceRoot");
        root.transform.SetParent(canvasRoot, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // Blocking overlay so result panel buttons can't be clicked
        var ov = new GameObject("Overlay");
        ov.transform.SetParent(root.transform, false);
        var ovRt = ov.AddComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
        var ovImg = ov.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 0.55f);
        ovImg.raycastTarget = true;

        // Choice panel background
        var bg = new GameObject("ChoicePanel");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(620f, 340f);
        bgRt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.98f);

        MakeLabel(bg, "ESCOLHA 1 BÔNUS:", 26, new Color(1f, 0.84f, 0f),
            new Vector2(0, 140f), new Vector2(580f, 38f), bold: true);

        MakeLevelUpCard(bg, opt1, new Vector2(-155f, 5f),
            () => { ApplyBonus(opt1, profile); Object.Destroy(root); onChosen(); });
        MakeLevelUpCard(bg, opt2, new Vector2( 155f, 5f),
            () => { ApplyBonus(opt2, profile); Object.Destroy(root); onChosen(); });
    }

    // TESTE: grade rolável com as opções disponíveis (4 atributos + skills com ícone já
    // re-adicionado em Assets/Data/UI/Skills/) — sem sorteio, escolhe livremente qualquer uma.
    // Armas removidas temporariamente da lista enquanto o teste foca em testar as skills uma
    // a uma (availableWeapons mantido como parâmetro, sem uso, pra reativar depois bastando
    // descomentar o foreach abaixo).
    private static void ShowAllOptionsChoice(Transform canvasRoot, PlayerProfile profile,
        List<SkillData> availableSkills, List<WeaponData> availableWeapons, System.Action onChosen)
    {
        var allOptions = new List<LevelUpOption>();
        for (int i = 0; i < 10; i++)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = i });
        foreach (var s in availableSkills)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = s });
        foreach (var w in availableWeapons)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = w });
        foreach (var pt in PetPool)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Pet, petType = pt });

        var root = new GameObject("LevelUpChoiceRoot");
        root.transform.SetParent(canvasRoot, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var ov = new GameObject("Overlay");
        ov.transform.SetParent(root.transform, false);
        var ovRt = ov.AddComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
        var ovImg = ov.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 0.75f);
        ovImg.raycastTarget = true;

        var bg = new GameObject("ChoicePanelAll");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(1100f, 700f);
        bgRt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.98f);

        MakeLabel(bg, $"[TESTE] ESCOLHA 1 BÔNUS ({allOptions.Count} opções):", 24,
            new Color(1f, 0.84f, 0f), new Vector2(0, 320f), new Vector2(1000f, 36f), bold: true);

        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(bg.transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(1060f, 580f);
        scrollRt.anchoredPosition = new Vector2(0, -20f);
        scrollGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.15f);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewport.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewportRt;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(170f, 200f);
        grid.spacing         = new Vector2(10f, 10f);
        grid.childAlignment  = TextAnchor.UpperCenter;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        foreach (var opt in allOptions)
        {
            var capturedOpt = opt;
            MakeLevelUpCard(content, capturedOpt, Vector2.zero,
                () => { ApplyBonus(capturedOpt, profile); Object.Destroy(root); onChosen(); });
        }
    }

    private static void MakeLevelUpCard(GameObject parent, LevelUpOption opt, Vector2 pos, System.Action onClick)
    {
        var card = new GameObject("Card");
        card.transform.SetParent(parent.transform, false);
        var rt = card.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(270f, 270f);
        rt.anchoredPosition = pos;
        card.AddComponent<Image>().color = new Color(0.09f, 0.09f, 0.22f, 1f);

        // Icon
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(card.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(80f, 80f);
        iconRt.anchoredPosition = new Vector2(0, 85f);
        var iconImg = iconGo.AddComponent<Image>();

        Sprite iconSprite = opt.kind switch {
            LevelUpOption.Kind.Skill   => opt.skill?.icon,
            LevelUpOption.Kind.Weapon  => opt.weapon?.inHandSprite,
            _                          => null
        };

        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.color  = Color.white;
        }
        else if (opt.kind == LevelUpOption.Kind.Pet)
        {
            // Sem sprite de preview próprio ainda — cor sólida só pra diferenciar visualmente
            // das demais categorias na grade de teste.
            iconImg.color = new Color(0.55f, 0.35f, 0.18f);
        }
        else
        {
            iconImg.color = opt.AttrColor();
        }

        MakeLabel(card, opt.Name(), 22, Color.white, new Vector2(0, 13f), new Vector2(250f, 34f), bold: true);
        MakeLabel(card, opt.Desc(), 16, new Color(0.78f, 0.78f, 0.78f), new Vector2(0, -28f), new Vector2(250f, 64f));

        var btn = MakeButton(card, "Escolher", new Vector2(0, -100f), onClick);
        btn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 44f);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IEnumerator AnimateBar(Image fill, float from, float to, float duration)
    {
        fill.fillAmount = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fill.fillAmount = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fill.fillAmount = to;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static Canvas FindScreenCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        return null;
    }

    private static void MakeOverlay(Transform parent)
    {
        var go = new GameObject("Overlay");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.65f);
        img.raycastTarget = false;
    }

    private static GameObject MakePanel(Transform parent)
    {
        var go = new GameObject("ResultPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 450f);
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.13f, 0.97f);
        return go;
    }

    private static TextMeshProUGUI MakeLabel(GameObject panel, string text, float fontSize,
        Color color, Vector2 pos, Vector2 size, bool bold = false)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    private static Image MakeXpBar(GameObject panel, Vector2 pos, Vector2 size)
    {
        var bg = new GameObject("XpBarBg");
        bg.transform.SetParent(panel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = size;
        bgRt.anchoredPosition = pos;
        bg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bg.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        var img = fillGo.AddComponent<Image>();
        img.color      = new Color(0.25f, 0.55f, 1f);
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 0f;
        return img;
    }

    private static Button MakeButton(GameObject panel, string label, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 54f);
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.13f, 0.42f, 0.13f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    private static bool IsInLoadout(WeaponData w, WeaponData[] loadout)
    {
        if (loadout == null || w == null) return false;
        foreach (var lw in loadout)
            if (lw != null && lw.weaponName == w.weaponName) return true;
        return false;
    }

    // Verifica se alguma versão de tier superior desta arma T1 já está no loadout
    private static bool HasUpgradeInLoadout(WeaponData t1, WeaponData[] loadout, WeaponData[] allWeapons)
    {
        if (allWeapons == null) return false;
        foreach (var candidate in allWeapons)
        {
            if (candidate == null || candidate.tier <= 1) continue;
            // T2 direto deste T1
            if (candidate.previousTier != null && candidate.previousTier.weaponName == t1.weaponName
                && IsInLoadout(candidate, loadout))
                return true;
            // T3 via cadeia T3→T2→T1
            if (candidate.previousTier?.previousTier != null
                && candidate.previousTier.previousTier.weaponName == t1.weaponName
                && IsInLoadout(candidate, loadout))
                return true;
        }
        return false;
    }
}
