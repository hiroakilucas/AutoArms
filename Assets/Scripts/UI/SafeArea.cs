using UnityEngine;

// Ajusta o próprio RectTransform pra caber dentro de Screen.safeArea (notch/câmera-furo/barra de
// gestos) — 2026-07-20, primeira vez que o projeto trata safe area (nenhuma outra Canvas do jogo
// faz isso ainda; dívida técnica registrada em ROADMAP_FUTURO.md, Fase 7 — uma adaptação mobile
// completa precisaria disso em toda tela, não só aqui). Escopo desta sessão: só os elementos do
// HUD principal ancorados em canto/borda de tela (moeda/diamante no canto superior direito,
// gaveta do personagem na parte inferior), a pedido do usuário.
//
// Padrão: um GameObject full-screen (anchorMin=0, anchorMax=1) recebe este componente; o
// conteúdo de verdade fica dentro dele, ancorado em frações relativas a ESTE retângulo (que já
// exclui a área insegura), não à tela crua.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        Apply();
    }

    // Reavalia todo frame (comparação de struct, barato) — cobre rotação em runtime e
    // dobráveis, que podem mudar Screen.safeArea sem trocar de cena.
    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
            Apply();
    }

    private void Apply()
    {
        _lastSafeArea = Screen.safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        Vector2 anchorMin = _lastSafeArea.position;
        Vector2 anchorMax = _lastSafeArea.position + _lastSafeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
    }
}
