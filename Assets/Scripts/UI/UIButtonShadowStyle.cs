using UnityEngine;
using UnityEngine.UI;

// Estilo visual reutilizável de "outline + drop shadow" pros botões principais do menu (JOGAR,
// Chibers, e qualquer outro que precise do mesmo tratamento) — usa os efeitos nativos de Unity UI
// (UnityEngine.UI.Outline/Shadow, ambos BaseMeshEffect aplicados sobre o Graphic do MESMO
// GameObject) em vez de sprite pré-renderizado com sombra, então funciona com qualquer Image já
// existente (inclusive as geradas em runtime por UIShapeUtil.RoundedRect). Não mexe na cor de
// fundo do botão nem no onClick — só adiciona os dois efeitos por cima. [ExecuteAlways] aplica o
// estilo direto no Editor (fora do Play Mode), pra dar pra calibrar cor/distância olhando o
// resultado na hora, igual editar os campos nativos de Shadow/Outline direto — só que com os
// dois já vindo configurados juntos com um valor padrão sensato.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class UIButtonShadowStyle : MonoBehaviour
{
    [Header("Outline (contorno)")]
    [SerializeField] private Color outlineColor = new Color(0.10f, 0.07f, 0.05f, 0.85f); // marrom escuro quase preto
    [SerializeField] private Vector2 outlineDistance = new Vector2(2f, -2f);
    [SerializeField] private bool outlineUseGraphicAlpha = true;

    [Header("Drop Shadow")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowDistance = new Vector2(0f, -3f);
    [SerializeField] private bool shadowUseGraphicAlpha = true;

    private Outline _outline;
    private Shadow _shadow;

    // AddComponent só roda aqui — nunca em OnValidate (o Unity lança "AddComponent cannot be
    // called during Awake, CheckConsistency, or OnValidate" se tentar; OnEnable não tem essa
    // restrição, e ExecuteAlways garante que ele já dispara ao adicionar o componente no Editor,
    // sem precisar dar Play).
    private void OnEnable()
    {
        EnsureEffects();
        Apply();
    }

    // Reaplica ao vivo quando qualquer campo é ajustado no Inspector — só atualiza os efeitos já
    // garantidos por OnEnable, nunca cria componente novo aqui.
    private void OnValidate()
    {
        if (_outline == null || _shadow == null) return;
        Apply();
    }

    private void EnsureEffects()
    {
        _outline = GetComponent<Outline>();
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();

        _shadow = FindPlainShadow();
        if (_shadow == null) _shadow = gameObject.AddComponent<Shadow>();
    }

    private void Apply()
    {
        _outline.effectColor = outlineColor;
        _outline.effectDistance = outlineDistance;
        _outline.useGraphicAlpha = outlineUseGraphicAlpha;

        _shadow.effectColor = shadowColor;
        _shadow.effectDistance = shadowDistance;
        _shadow.useGraphicAlpha = shadowUseGraphicAlpha;
    }

    // GetComponent<Shadow>() também casaria com um Outline já existente no mesmo GameObject —
    // Outline herda de Shadow em UnityEngine.UI, então uma busca genérica arriscava devolver o
    // Outline (efeito errado) em vez de uma instância de Shadow "pura". Procura pelo tipo exato.
    private Shadow FindPlainShadow()
    {
        foreach (var s in GetComponents<Shadow>())
            if (s.GetType() == typeof(Shadow)) return s;
        return null;
    }
}
