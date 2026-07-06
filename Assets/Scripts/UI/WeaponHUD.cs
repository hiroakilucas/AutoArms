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
    private readonly HashSet<string> _sabotagedNames = new HashSet<string>();

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

    // Spy: chamado pelo CombatSceneLoader depois de CombatSimulator.Simulate() rodar — armas
    // sabotadas (nomes, ver CombatSimulator.Player1SabotagedWeapons/Player2SabotagedWeapons)
    // ficam com o ícone vermelho em vez do preto semi-transparente padrão, mesmo quando não
    // estão equipadas. Não precisa de Rebuild — só reaplica a cor dos ícones já existentes.
    public void SetSabotagedWeapons(IEnumerable<string> weaponNames)
    {
        _sabotagedNames.Clear();
        if (weaponNames != null)
            foreach (var n in weaponNames)
                _sabotagedNames.Add(n);
        UpdateHighlight();
    }

    // Saboteur: posição em tela (Screen Space Overlay, então transform.position já é em
    // pixels) do ícone da arma destruída, pra CombatPlayer poder spawnar a queda visual
    // saindo exatamente de cima do ícone — null se a arma não estiver mais no HUD (já caiu,
    // ou nunca apareceu por falta de inHandSprite).
    public Vector3? GetIconScreenPosition(string weaponName)
    {
        foreach (var kvp in _iconMap)
        {
            if (kvp.Value != null && kvp.Key != null && kvp.Key.weaponName == weaponName)
                return kvp.Value.transform.position;
        }
        return null;
    }

    // Saboteur: remove o ícone permanentemente do HUD (a arma nunca foi equipada, então
    // RemoveCurrentWeapon's fallback por currentIndex não serve — precisa do `expected`
    // explícito pra achar a entrada certa por referência, independente do que está em mãos).
    public void RemoveWeapon(WeaponData data) => _loadout.RemoveCurrentWeapon(data);

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

    private const float IconSize = 100f;

    private GameObject BuildIcon(WeaponData data)
    {
        var go = new GameObject(data.weaponName ?? "WeaponIcon");
        go.transform.SetParent(_container, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(IconSize, IconSize);

        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(go.transform, false);
        var img = spriteGo.AddComponent<Image>();
        img.sprite = data.inHandSprite;
        img.preserveAspect = true;
        var spriteRt = spriteGo.GetComponent<RectTransform>();
        spriteRt.anchorMin = new Vector2(0.5f, 0.5f);
        spriteRt.anchorMax = new Vector2(0.5f, 0.5f);
        spriteRt.pivot     = new Vector2(0.5f, 0.5f);
        spriteRt.sizeDelta = new Vector2(IconSize, IconSize);
        spriteRt.anchoredPosition = Vector2.zero;
        spriteGo.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

        return go;
    }

    private void UpdateHighlight()
    {
        // PinnedHudWeapon cobre o intervalo entre arremessos repetidos no mesmo turno (hitSpeed
        // alto), onde CurrentWeaponData fica momentaneamente null — sem isso o ícone piscava
        // cinza a cada ciclo em vez de ficar dourado o tempo todo (ver WeaponHandler).
        var active = _handler.CurrentWeaponData ?? _handler.PinnedHudWeapon;
        foreach (var kvp in _iconMap)
        {
            if (kvp.Value == null) continue;
            bool isActive    = kvp.Key == active;
            bool isSabotaged = kvp.Key.weaponName != null && _sabotagedNames.Contains(kvp.Key.weaponName);
            Color color;
            if (isActive)
                color = new Color(0.85f, 0.72f, 0.20f, 0.70f);
            else if (isSabotaged)
                color = new Color(0.75f, 0.15f, 0.15f, 0.55f);
            else
                color = new Color(0f, 0f, 0f, 0.35f);
            kvp.Value.GetComponent<Image>().color = color;
        }
    }

    private GameObject BuildContainer(Transform parent)
    {
        var go = new GameObject(_isPlayer1 ? "P1WeaponHUD" : "P2WeaponHUD");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        if (_isPlayer1)
        {
            rt.anchorMin = new Vector2(0.02f, 0.810f);
            rt.anchorMax = new Vector2(0.45f, 0.930f);
        }
        else
        {
            rt.anchorMin = new Vector2(0.55f, 0.810f);
            rt.anchorMax = new Vector2(0.98f, 0.930f);
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
