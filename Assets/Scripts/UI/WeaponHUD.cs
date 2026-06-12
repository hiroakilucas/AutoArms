using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponHUD : MonoBehaviour
{
    private PlayerLoadout _loadout;
    private WeaponHandler _handler;
    private bool _isPlayer1;
    private Transform _container;
    private readonly Dictionary<WeaponData, GameObject> _iconMap = new Dictionary<WeaponData, GameObject>();

    public void Initialize(PlayerLoadout loadout, WeaponHandler handler, bool isPlayer1, Transform canvasTransform)
    {
        _loadout   = loadout;
        _handler   = handler;
        _isPlayer1 = isPlayer1;
        _container = BuildContainer(canvasTransform).transform;

        loadout.OnWeaponsChanged += Rebuild;
        handler.OnWeaponChanged  += OnWeaponHandlerChanged;

        Rebuild();
    }

    private void OnDestroy()
    {
        if (_loadout != null) _loadout.OnWeaponsChanged  -= Rebuild;
        if (_handler != null) _handler.OnWeaponChanged   -= OnWeaponHandlerChanged;
    }

    private void OnWeaponHandlerChanged(WeaponData _) => UpdateHighlight();

    private void Rebuild()
    {
        foreach (var icon in _iconMap.Values)
            if (icon) Destroy(icon);
        _iconMap.Clear();

        var weapons = _loadout.Weapons;

        // P1: left→right (weapon[0] leftmost)
        // P2: added in reverse so weapon[0] is rightmost within the right-aligned group
        if (_isPlayer1)
        {
            for (int i = 0; i < weapons.Count; i++)
                TryAddIcon(weapons[i]);
        }
        else
        {
            for (int i = weapons.Count - 1; i >= 0; i--)
                TryAddIcon(weapons[i]);
        }

        UpdateHighlight();
    }

    private void TryAddIcon(WeaponData data)
    {
        if (data?.inHandSprite == null) return;
        if (_iconMap.ContainsKey(data)) return;
        _iconMap[data] = BuildIcon(data);
    }

    private GameObject BuildIcon(WeaponData data)
    {
        var go = new GameObject(data.weaponName ?? "WeaponIcon");
        go.transform.SetParent(_container, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(24f, 24f);

        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(go.transform, false);
        var img = spriteGo.AddComponent<Image>();
        img.sprite = data.inHandSprite;
        img.preserveAspect = true;
        var spriteRt = spriteGo.GetComponent<RectTransform>();
        spriteRt.anchorMin = new Vector2(0.08f, 0.08f);
        spriteRt.anchorMax = new Vector2(0.92f, 0.92f);
        spriteRt.offsetMin = Vector2.zero;
        spriteRt.offsetMax = Vector2.zero;

        return go;
    }

    private void UpdateHighlight()
    {
        var active = _handler.CurrentWeaponData;
        foreach (var kvp in _iconMap)
        {
            if (kvp.Value == null) continue;
            bool isActive = kvp.Key == active;
            kvp.Value.GetComponent<Image>().color = isActive
                ? new Color(0.85f, 0.72f, 0.20f, 0.85f)
                : new Color(0f, 0f, 0f, 0.60f);
        }
    }

    private GameObject BuildContainer(Transform parent)
    {
        var go = new GameObject(_isPlayer1 ? "P1WeaponHUD" : "P2WeaponHUD");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        if (_isPlayer1)
        {
            rt.anchorMin = new Vector2(0.02f, 0.87f);
            rt.anchorMax = new Vector2(0.45f, 0.93f);
        }
        else
        {
            rt.anchorMin = new Vector2(0.55f, 0.87f);
            rt.anchorMax = new Vector2(0.98f, 0.93f);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 2f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        // P1: icons accumulate from left edge; P2: icons accumulate from right edge
        hlg.childAlignment = _isPlayer1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

        return go;
    }
}
