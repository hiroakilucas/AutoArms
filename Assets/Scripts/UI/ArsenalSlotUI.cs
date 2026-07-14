using UnityEngine;
using UnityEngine.UI;

// Slot reutilizável (ícone + borda por tier + estado bloqueado/escurecido) usado tanto pela
// grade de ARMAS quanto pela de SKILLS em 03_Arsenal (ArsenalController) — evita duplicar a
// lógica de borda/estado entre as duas grades. Construído 100% via código com
// UIShapeUtil.RoundedRect (mesmo padrão de CharacterPanel/CharacterCardUI), sem depender de
// prefab nem de sprites de borda prontos — bronze/prata/ouro são cor sólida via UITheme.tierBronze/
// tierSilver/tierGold (mesmos tokens já usados no popup de detalhe de skill/arma do
// CharacterPanel) até existir arte definitiva.
public class ArsenalSlotUI : MonoBehaviour
{
    public enum Tier { None, T1, T2, T3 }

    private const float BorderPadding = 6f; // espessura da borda visível ao redor do ícone
    private const float CornerRadius = 14f;

    // Cor da borda quando o personagem não possui a arma/skill em nenhum tier — cinza neutro,
    // claramente distinto de bronze/prata/ouro.
    private static readonly Color NoTierBorderColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);

    // Tint quase preto no ícone bloqueado (mesmo valor usado por CharacterCardUI pro portrait de
    // personagem travado, ver BuildPortraitBox) + overlay escuro semi-transparente por cima —
    // aproximação de "grayscale escurecido" sem precisar de shader custom pra desaturação de
    // verdade (nenhuma outra parte do projeto usa shader de grayscale; tint quase preto é o
    // padrão já estabelecido pra "bloqueado"). Usado só quando `dimIcon=true` (armas, ver Build).
    private static readonly Color LockedIconTint = new Color(0.06f, 0.06f, 0.06f, 1f);
    private static readonly Color LockedOverlayColor = new Color(0f, 0f, 0f, 0.55f);

    // Sombra pra `dimIcon=false` (skills): ícone mantém a cor original (sem tint quase-preto),
    // só um véu por cima — 0.5 (2026-07-14) ficou "muito claro", 0.72 e 0.90 ainda não o
    // bastante; 0.96 (pedido do usuário, mesmo dia) é a opacidade final.
    private static readonly Color LightLockedOverlayColor = new Color(0f, 0f, 0f, 0.96f);

    // onClick (2026-07-14, pedido do usuário) — opcional; quando presente, o slot inteiro fica
    // clicável através do próprio `Border` (cobre 100% do rect) INDEPENDENTE do tier/estado
    // bloqueado, pra deixar ver os atributos de qualquer arma/skill mesmo sem possuí-la ainda.
    // `dimIcon` (2026-07-14, default true = comportamento de sempre): armas continuam com o
    // ícone tingido quase-preto quando bloqueado; skills usam `dimIcon=false` (ícone na cor
    // original + só a sombra de 50%, bem mais claro).
    public void Build(UITheme theme, Sprite icon, Tier tier, System.Action onClick = null, bool dimIcon = true)
    {
        var rt = (RectTransform)transform;
        bool locked = tier == Tier.None;

        var borderGo = new GameObject("Border", typeof(RectTransform));
        var borderRt = (RectTransform)borderGo.transform;
        borderRt.SetParent(rt, false);
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = UIShapeUtil.RoundedRect(TierColor(theme, tier), CornerRadius);
        borderImg.type = Image.Type.Sliced;

        if (onClick != null)
        {
            var btn = borderGo.AddComponent<Button>();
            btn.targetGraphic = borderImg;
            btn.onClick.AddListener(() => onClick());
        }

        var iconBgGo = new GameObject("IconBg", typeof(RectTransform));
        var iconBgRt = (RectTransform)iconBgGo.transform;
        iconBgRt.SetParent(rt, false);
        iconBgRt.anchorMin = Vector2.zero;
        iconBgRt.anchorMax = Vector2.one;
        iconBgRt.offsetMin = new Vector2(BorderPadding, BorderPadding);
        iconBgRt.offsetMax = new Vector2(-BorderPadding, -BorderPadding);
        var iconBgImg = iconBgGo.AddComponent<Image>();
        iconBgImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackground, CornerRadius * 0.7f);
        iconBgImg.type = Image.Type.Sliced;
        // raycastTarget=false (bug real, 2026-07-14): IconBg/Icon são desenhados DEPOIS de
        // Border (siblings mais recentes = na frente), e por padrão todo Image tem
        // raycastTarget=true — o clique batia neles primeiro e nunca chegava no Button do
        // Border, deixando o slot inteiro sem reagir a clique nenhum. Só Border precisa ser
        // clicável.
        iconBgImg.raycastTarget = false;

        // Sem ícone (arte ainda não gerada pra essa arma/skill, ver CHARACTER_IMPORT_CHECKLIST.md/
        // SKILLS_SYSTEM.md) — deixa só o fundo+borda, sem Image quebrada.
        if (icon != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(iconBgRt, false);
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.color = (locked && dimIcon) ? LockedIconTint : Color.white;
            iconImg.raycastTarget = false; // mesmo motivo do IconBg acima
        }

        if (locked)
        {
            var overlayGo = new GameObject("LockOverlay", typeof(RectTransform));
            var overlayRt = (RectTransform)overlayGo.transform;
            overlayRt.SetParent(iconBgRt, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.sprite = UIShapeUtil.RoundedRect(dimIcon ? LockedOverlayColor : LightLockedOverlayColor, CornerRadius * 0.7f);
            overlayImg.type = Image.Type.Sliced;
            overlayImg.raycastTarget = false;
        }
    }

    private static Color TierColor(UITheme theme, Tier tier) => tier switch
    {
        Tier.T1 => theme.tierBronze,
        Tier.T2 => theme.tierSilver,
        Tier.T3 => theme.tierGold,
        _ => NoTierBorderColor,
    };
}
