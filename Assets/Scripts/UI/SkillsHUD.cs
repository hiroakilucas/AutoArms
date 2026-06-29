using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD abaixo do WeaponHUD: exibe skills ativas (activationType == Active) com contador de usos.
// Ícone some quando usos chegam a 0; contador omitido quando usesPerFight == 0 (ilimitado).
// Atualizado por CombatPlayer via UseSkill() em cada evento de Super.
public class SkillsHUD : MonoBehaviour
{
    private bool          _isPlayer1;
    private RectTransform _container;

    private readonly Dictionary<string, SkillSlot> _slots = new Dictionary<string, SkillSlot>();

    private struct SkillSlot
    {
        public GameObject      root;
        public int             uses;      // 0 = ilimitado; >0 = usos restantes
        public TextMeshProUGUI usesText;
    }

    public void Initialize(List<SkillData> skills, bool isPlayer1, Transform canvasTransform)
    {
        _isPlayer1 = isPlayer1;
        BuildContainer(canvasTransform);

        if (skills == null) return;

        var toAdd = new List<SkillData>();
        foreach (var sk in skills)
        {
            if (sk == null || sk.activationType != SkillActivationType.Active) continue;
            toAdd.Add(sk);
        }

        // P2: reverse para que skill[0] fique mais à direita (mesma convenção do WeaponHUD)
        if (!isPlayer1) toAdd.Reverse();

        foreach (var sk in toAdd)
            AddIcon(sk);
    }

    // Chamado por CombatPlayer quando um evento de Super dispara para o playerIndex.
    // Decrementa usos; remove ícone quando chega a 0.
    public void UseSkill(string skillName)
    {
        if (!_slots.TryGetValue(skillName, out var slot)) return;
        if (slot.uses == 0) return; // usesPerFight == 0 → ilimitado, nunca remove

        int remaining = slot.uses - 1;
        if (remaining <= 0)
        {
            if (slot.root != null) Destroy(slot.root);
            _slots.Remove(skillName);
        }
        else
        {
            if (slot.usesText != null) slot.usesText.text = remaining.ToString();
            _slots[skillName] = new SkillSlot { root = slot.root, uses = remaining, usesText = slot.usesText };
        }
    }

    private void BuildContainer(Transform canvasTransform)
    {
        var go = new GameObject(_isPlayer1 ? "P1SkillsHUD" : "P2SkillsHUD");
        go.transform.SetParent(canvasTransform, false);
        _container = go.AddComponent<RectTransform>();

        // Banda Y 0.700–0.810: logo abaixo do WeaponHUD (0.810–0.930)
        if (_isPlayer1)
        {
            _container.anchorMin = new Vector2(0.02f, 0.700f);
            _container.anchorMax = new Vector2(0.45f, 0.810f);
        }
        else
        {
            _container.anchorMin = new Vector2(0.55f, 0.700f);
            _container.anchorMax = new Vector2(0.98f, 0.810f);
        }
        _container.offsetMin = Vector2.zero;
        _container.offsetMax = Vector2.zero;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment        = _isPlayer1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        layout.spacing               = 4f;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.padding               = new RectOffset(4, 4, 4, 4);
    }

    private void AddIcon(SkillData skill)
    {
        const float iconSize = 80f;

        var root = new GameObject(skill.skillName);
        root.transform.SetParent(_container, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(iconSize, iconSize);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        if (skill.icon != null)
        {
            var sprGo = new GameObject("Icon");
            sprGo.transform.SetParent(root.transform, false);
            var sprRect = sprGo.AddComponent<RectTransform>();
            sprRect.anchorMin = new Vector2(0.05f, 0.05f);
            sprRect.anchorMax = new Vector2(0.95f, 0.95f);
            sprRect.offsetMin = sprRect.offsetMax = Vector2.zero;
            var img = sprGo.AddComponent<Image>();
            img.sprite = skill.icon;
            img.preserveAspect = true;
        }

        TextMeshProUGUI usesText = null;
        if (skill.usesPerFight > 0)
        {
            var tGo = new GameObject("Uses");
            tGo.transform.SetParent(root.transform, false);
            var tRect = tGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.55f, 0f);
            tRect.anchorMax = new Vector2(1f, 0.45f);
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            usesText = tGo.AddComponent<TextMeshProUGUI>();
            usesText.text      = skill.usesPerFight.ToString();
            usesText.fontSize  = 18f;
            usesText.fontStyle = FontStyles.Bold;
            usesText.alignment = TextAlignmentOptions.BottomRight;
            usesText.color     = Color.white;
        }

        _slots[skill.skillName] = new SkillSlot { root = root, uses = skill.usesPerFight, usesText = usesText };
    }
}
